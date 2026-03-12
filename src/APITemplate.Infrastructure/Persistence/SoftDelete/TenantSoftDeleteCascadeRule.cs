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

        var users = await dbContext
            .Users.IgnoreQueryFilters(["SoftDelete", "Tenant"])
            .Where(u => u.TenantId == tenant.Id && !u.IsDeleted)
            .Cast<IAuditableTenantEntity>()
            .ToListAsync(cancellationToken);

        return users;
    }
}
