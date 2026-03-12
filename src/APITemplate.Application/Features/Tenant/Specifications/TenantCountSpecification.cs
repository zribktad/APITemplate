using APITemplate.Application.Features.Tenant.DTOs;
using Ardalis.Specification;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant.Specifications;

public sealed class TenantCountSpecification : Specification<TenantEntity>
{
    public TenantCountSpecification(TenantFilter filter)
    {
        TenantFilterCriteria.Apply(Query, filter);
        Query.AsNoTracking();
    }
}
