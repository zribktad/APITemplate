using APITemplate.Application.Features.Tenant.DTOs;
using APITemplate.Application.Features.Tenant.Mappings;
using Ardalis.Specification;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant.Specifications;

public sealed class TenantByIdSpecification : Specification<TenantEntity, TenantResponse>
{
    public TenantByIdSpecification(Guid id)
    {
        Query.Where(tenant => tenant.Id == id).AsNoTracking().Select(TenantMappings.Projection);
    }
}
