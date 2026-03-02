using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class ProductCountSpecification : Specification<Product>
{
    public ProductCountSpecification(ProductFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Name))
            Query.Where(p => p.Name.Contains(filter.Name));

        if (!string.IsNullOrWhiteSpace(filter.Description))
            Query.Where(p => p.Description != null && p.Description.Contains(filter.Description));

        if (filter.MinPrice.HasValue)
            Query.Where(p => p.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            Query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.CreatedFrom.HasValue)
            Query.Where(p => p.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            Query.Where(p => p.CreatedAt <= filter.CreatedTo.Value);
    }
}