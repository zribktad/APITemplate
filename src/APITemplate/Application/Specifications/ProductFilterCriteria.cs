using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

internal static class ProductFilterCriteria
{
    internal static void Apply(ISpecificationBuilder<Product> query, ProductFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Name))
            query.Where(p => p.Name.Contains(filter.Name));

        if (!string.IsNullOrWhiteSpace(filter.Description))
            query.Where(p => p.Description != null && p.Description.Contains(filter.Description));

        if (filter.MinPrice.HasValue)
            query.Where(p => p.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(p => p.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(p => p.CreatedAt <= filter.CreatedTo.Value);
    }
}
