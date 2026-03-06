using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Persistence;

public sealed class AuthBootstrapSeeder
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AppDbContext _dbContext;
    private readonly BootstrapTenantOptions _tenantOptions;

    public AuthBootstrapSeeder(
        AppDbContext dbContext,
        IOptions<BootstrapTenantOptions> tenantOptions)
    {
        _dbContext = dbContext;
        _tenantOptions = tenantOptions.Value;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var hasChanges = false;

        var tenantCode = _tenantOptions.Code.Trim();
        var tenantName = _tenantOptions.Name.Trim();

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Code == tenantCode, ct);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = DefaultTenantId,
                TenantId = Guid.Empty,
                Code = tenantCode,
                Name = tenantName,
                IsActive = true
            };

            _dbContext.Tenants.Add(tenant);
            hasChanges = true;
        }
        else
        {
            if (!tenant.IsActive)
            {
                tenant.IsActive = true;
                hasChanges = true;
            }

            if (tenant.IsDeleted)
            {
                tenant.IsDeleted = false;
                tenant.DeletedAtUtc = null;
                tenant.DeletedBy = null;
                hasChanges = true;
            }
        }

        if (hasChanges)
            await _dbContext.SaveChangesAsync(ct);
    }
}
