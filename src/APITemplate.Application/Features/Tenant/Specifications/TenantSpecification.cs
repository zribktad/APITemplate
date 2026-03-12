using APITemplate.Application.Features.Tenant.DTOs;
using APITemplate.Application.Features.Tenant.Mappings;
using Ardalis.Specification;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant.Specifications;

public sealed class TenantSpecification : Specification<TenantEntity, TenantResponse>
{
    public TenantSpecification(TenantFilter filter)
    {
        TenantFilterCriteria.Apply(Query, filter);
        Query.AsNoTracking();
        TenantSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);
        Query.Select(TenantMappings.Projection);
        Query.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize);
    }
}
