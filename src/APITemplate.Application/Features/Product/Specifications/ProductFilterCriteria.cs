using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;
internal static class ProductFilterCriteria
{
    private const string SearchConfiguration = "english";

    internal static void Apply(
        ISpecificationBuilder<ProductEntity> query,
        ProductFilter filter,
        ProductFilterCriteriaOptions? options = null)
    {
        options ??= ProductFilterCriteriaOptions.Default;

        if (!string.IsNullOrWhiteSpace(filter.Name))
            query.Where(p => p.Name.Contains(filter.Name));

        if (!string.IsNullOrWhiteSpace(filter.Description))
            query.Where(p => p.Description != null && p.Description.Contains(filter.Description));

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            query.Where(p =>
                EF.Functions
                    .ToTsVector(SearchConfiguration, p.Name + " " + (p.Description ?? string.Empty))
                    .Matches(EF.Functions.WebSearchToTsQuery(SearchConfiguration, filter.Query)));
        }

        if (!options.IgnorePriceRange && filter.MinPrice.HasValue)
            query.Where(p => p.Price >= filter.MinPrice.Value);

        if (!options.IgnorePriceRange && filter.MaxPrice.HasValue)
            query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(p => p.Audit.CreatedAtUtc >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(p => p.Audit.CreatedAtUtc <= filter.CreatedTo.Value);

        if (!options.IgnoreCategoryIds && filter.CategoryIds is { Count: > 0 })
            query.Where(p => p.CategoryId.HasValue && filter.CategoryIds.Contains(p.CategoryId.Value));
    }
}

internal sealed record ProductFilterCriteriaOptions(
    bool IgnoreCategoryIds = false,
    bool IgnorePriceRange = false)
{
    internal static ProductFilterCriteriaOptions Default => new();
}
