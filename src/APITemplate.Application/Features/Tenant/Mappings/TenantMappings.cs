using System.Linq.Expressions;
using APITemplate.Application.Features.Tenant.DTOs;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant.Mappings;

public static class TenantMappings
{
    public static readonly Expression<Func<TenantEntity, TenantResponse>> Projection =
        tenant => new TenantResponse(
            tenant.Id,
            tenant.Code,
            tenant.Name,
            tenant.IsActive,
            tenant.Audit.CreatedAtUtc
        );

    private static readonly Func<TenantEntity, TenantResponse> CompiledProjection =
        Projection.Compile();

    public static TenantResponse ToResponse(this TenantEntity tenant) => CompiledProjection(tenant);
}
