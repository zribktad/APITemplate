using APITemplate.Application.Common.Context;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Main EF Core context for relational storage.
/// Enforces multi-tenancy, audit stamping, soft delete, and optimistic concurrency
/// for all entities based on <see cref="IAuditableTenantEntity"/>.
/// </summary>
/// <remarks>
/// Key behavior:
/// <list type="bullet">
/// <item>
/// <description>
/// Global query filters automatically limit reads to the current tenant and exclude soft-deleted rows.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="SaveChanges(bool)"/> and <see cref="SaveChangesAsync(bool, CancellationToken)"/> centralize
/// audit field updates (<c>Created*</c>/<c>Updated*</c>/<c>Deleted*</c>).
/// </description>
/// </item>
/// <item>
/// <description>
/// Delete operations are converted to soft delete updates, including soft-cascade from Product to ProductReviews.
/// </description>
/// </item>
/// </list>
/// </remarks>
public sealed class AppDbContext : DbContext
{
    // Tenant provider drives read isolation (global filters) and default tenant assignment on inserts.
    private readonly ITenantProvider _tenantProvider;
    private readonly IActorProvider _actorProvider;
    // Explicit soft-delete cascade rules registered via DI.
    private readonly IReadOnlyCollection<ISoftDeleteCascadeRule> _softDeleteCascadeRules;

    private Guid CurrentTenantId => _tenantProvider.TenantId;
    private bool HasTenant => _tenantProvider.HasTenant;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider) : this(options, tenantProvider, actorProvider, [])
    {
    }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        IEnumerable<ISoftDeleteCascadeRule> softDeleteCascadeRules) : base(options)
    {
        _tenantProvider = tenantProvider;
        _actorProvider = actorProvider;
        _softDeleteCascadeRules = softDeleteCascadeRules.ToList();
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();

    /// <summary>
    /// Keyless entity — no backing table.
    /// Materialised only via <c>FromSql()</c> when calling the stored procedure.
    /// </summary>
    public DbSet<ProductCategoryStats> ProductCategoryStats => Set<ProductCategoryStats>();

    /// <summary>
    /// Applies entity configurations and auto-registers global tenant/soft-delete query filters.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
    }

    /// <summary>
    /// Applies audit/soft-delete rules before committing changes.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyEntityAuditing();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Applies audit/soft-delete rules before committing changes asynchronously.
    /// </summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyEntityAuditing();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Discovers all model types implementing tenant + soft-delete contracts
    /// and wires a generic global filter for each of them.
    /// </summary>
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType) ||
                !typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(AppDbContext)
                .GetMethod(nameof(SetGlobalFilter), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetGlobalFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity, ISoftDeletable
    {
        // When HasTenant is false, tenant-scoped entities are intentionally filtered out (safe default for non-tenant contexts).
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => !entity.IsDeleted && HasTenant && entity.TenantId == CurrentTenantId);
    }

    /// <summary>
    /// Processes tracked entities and stamps audit fields according to current state.
    /// </summary>
    private void ApplyEntityAuditing()
    {
        var now = DateTime.UtcNow;
        var actor = _actorProvider.ActorId;

        foreach (var entry in ChangeTracker.Entries().Where(e => e.Entity is IAuditableTenantEntity))
        {
            var entity = (IAuditableTenantEntity)entry.Entity;
            switch (entry.State)
            {
                case EntityState.Added:
                    EnsureEntityNormalization(entity);
                    StampAddedEntity(entry, entity, now, actor);
                    break;
                case EntityState.Modified:
                    EnsureEntityNormalization(entity);
                    MarkUpdated(entity, now, actor);
                    break;
                case EntityState.Deleted:
                    StampSoftDeletedEntity(entry, entity, now, actor);
                    break;
            }
        }
    }

    /// <summary>
    /// Fills creation/update metadata for newly added entities and assigns tenant identity when missing.
    /// </summary>
    private void StampAddedEntity(EntityEntry entry, IAuditableTenantEntity entity, DateTime now, string actor)
    {
        if (entity is Tenant tenant && tenant.TenantId == Guid.Empty)
            tenant.TenantId = tenant.Id;

        // Auto-stamp tenant ownership for new tenant-bound rows when request context provides tenant identity.
        if (entity.TenantId == Guid.Empty && HasTenant)
            entity.TenantId = CurrentTenantId;

        entity.Audit.CreatedAtUtc = now;
        entity.Audit.CreatedBy = actor;
        MarkUpdated(entity, now, actor);
        ResetSoftDelete(entity);
        entry.State = EntityState.Added;
    }

    /// <summary>
    /// Converts a hard delete into a soft delete update and performs soft-cascade for dependent rows.
    /// </summary>
    private void StampSoftDeletedEntity(EntityEntry entry, IAuditableTenantEntity entity, DateTime now, string actor)
    {
        var visited = new HashSet<IAuditableTenantEntity>(ReferenceEqualityComparer.Instance);
        SoftDeleteWithRules(entry, entity, now, actor, visited);
    }

    /// <summary>
    /// Performs explicit soft-delete cascade based on registered rules.
    /// Each rule describes its own dependent graph (no implicit navigation traversal).
    /// </summary>
    private void SoftDeleteWithRules(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        string actor,
        HashSet<IAuditableTenantEntity> visited)
    {
        if (!visited.Add(entity))
            return;

        entry.State = EntityState.Modified;
        MarkSoftDeleted(entity, now, actor);
        EnsureAuditOwnedEntryState(entry, now, actor);

        foreach (var rule in _softDeleteCascadeRules.Where(r => r.CanHandle(entity)))
        {
            var dependents = rule.GetDependents(this, entity);
            foreach (var dependent in dependents)
            {
                if (dependent.IsDeleted || dependent.TenantId != entity.TenantId)
                    continue;

                var dependentEntry = Entry(dependent);
                SoftDeleteWithRules(dependentEntry, dependent, now, actor, visited);
            }
        }
    }

    /// <summary>
    /// When entity state transitions from Deleted to Modified, EF can keep owned entries in Deleted state.
    /// For table-split owned Audit this would null-out required columns on update.
    /// Force the owned Audit entry back to Modified and stamp updated metadata.
    /// </summary>
    private static void EnsureAuditOwnedEntryState(EntityEntry ownerEntry, DateTime now, string actor)
    {
        var auditEntry = ownerEntry.Reference(nameof(IAuditableTenantEntity.Audit)).TargetEntry;
        if (auditEntry is null)
            return;

        if (auditEntry.State is EntityState.Deleted or EntityState.Detached or EntityState.Unchanged)
            auditEntry.State = EntityState.Modified;

        auditEntry.Property(nameof(AuditInfo.UpdatedAtUtc)).CurrentValue = now;
        auditEntry.Property(nameof(AuditInfo.UpdatedBy)).CurrentValue = actor;
    }

    private static void MarkUpdated(IAuditableTenantEntity entity, DateTime now, string actor)
    {
        entity.Audit.UpdatedAtUtc = now;
        entity.Audit.UpdatedBy = actor;
        // App-managed optimistic concurrency token; DB triggers are intentionally disabled.
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private static void EnsureEntityNormalization(IAuditableTenantEntity entity)
    {
        if (entity is AppUser user)
            user.NormalizedUsername = NormalizeUsername(user.Username);
    }

    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    private static void ResetSoftDelete(IAuditableTenantEntity entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAtUtc = null;
        entity.DeletedBy = null;
    }

    private static void MarkSoftDeleted(IAuditableTenantEntity entity, DateTime now, string actor)
    {
        entity.IsDeleted = true;
        entity.DeletedAtUtc = now;
        entity.DeletedBy = actor;
        MarkUpdated(entity, now, actor);
    }
}
