using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

// Generic base repository — T is constrained to class (reference type) so EF Core can track it.
// abstract = cannot be instantiated directly, must be inherited (e.g. UserRepository : RepositoryBase<User>).
// virtual methods allow derived repos to override individual operations when needed.
public abstract class RepositoryBase<T> : IRepository<T> where T : class
{
    // Shared DbContext injected via constructor — all derived repos use the same instance per request (scoped DI lifetime).
    protected readonly AppDbContext DbContext;

    protected RepositoryBase(AppDbContext dbContext)
    {
        DbContext = dbContext;
    }

    // FindAsync hits the EF Identity Map first (in-memory cache of tracked entities),
    // so if T was already loaded in this request, no DB query is issued.
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Params array syntax [id] required by EF Core 8 overload that accepts CancellationToken alongside keys.
        return await DbContext.Set<T>().FindAsync([id], ct);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<T>()
            // AsNoTracking skips the change-tracker overhead — safe here because we only read, never mutate.
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        // Add stages the entity with EntityState.Added — no DB hit yet.
        DbContext.Set<T>().Add(entity);
        // SaveChangesAsync flushes all pending changes in a single transaction and generates the INSERT.
        await DbContext.SaveChangesAsync(ct);
        // Return the same entity so callers get DB-generated values (e.g. auto-increment Id, default columns).
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        // Update marks every scalar property as Modified — issues a full UPDATE even for unchanged columns.
        // For partial updates consider Attach + explicit property marking instead.
        DbContext.Set<T>().Update(entity);
        await DbContext.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.Set<T>().FindAsync([id], ct);

        // Guard clause — silently no-ops if the entity was already deleted or never existed.
        if (entity is null)
            return;

        // Remove sets EntityState.Deleted; SaveChangesAsync translates it to a DELETE statement.
        DbContext.Set<T>().Remove(entity);
        await DbContext.SaveChangesAsync(ct);
    }
}
