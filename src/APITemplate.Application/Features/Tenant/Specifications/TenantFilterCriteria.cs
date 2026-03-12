using APITemplate.Application.Features.Tenant.DTOs;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant.Specifications;

internal static class TenantFilterCriteria
{
    private const string SearchConfiguration = "english";

    internal static void Apply(ISpecificationBuilder<TenantEntity> query, TenantFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Query))
            return;

        query.Where(tenant =>
            EF.Functions.ToTsVector(SearchConfiguration, tenant.Code + " " + tenant.Name)
                .Matches(EF.Functions.WebSearchToTsQuery(SearchConfiguration, filter.Query))
        );
    }
}
