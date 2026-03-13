using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Persistence.SoftDelete;

public sealed class TenantSoftDeleteCascadeRule : ISoftDeleteCascadeRule
{
    public bool CanHandle(IAuditableTenantEntity entity) => entity is Tenant;

    public async Task<IReadOnlyCollection<IAuditableTenantEntity>> GetDependentsAsync(
        AppDbContext dbContext,
        IAuditableTenantEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        if (entity is not Tenant tenant)
            return [];

        var dependents = new List<IAuditableTenantEntity>();

        dependents.AddRange(
            await dbContext
                .Users.IgnoreQueryFilters(["SoftDelete", "Tenant"])
                .Where(u => u.TenantId == tenant.Id && !u.IsDeleted)
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        dependents.AddRange(
            await dbContext
                .Products.IgnoreQueryFilters(["SoftDelete", "Tenant"])
                .Where(p => p.TenantId == tenant.Id && !p.IsDeleted)
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        dependents.AddRange(
            await dbContext
                .Categories.IgnoreQueryFilters(["SoftDelete", "Tenant"])
                .Where(c => c.TenantId == tenant.Id && !c.IsDeleted)
                .Cast<IAuditableTenantEntity>()
                .ToListAsync(cancellationToken)
        );

        return dependents;
    }
}
