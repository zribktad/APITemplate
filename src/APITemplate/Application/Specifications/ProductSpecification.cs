using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class ProductSpecification : Specification<Product, ProductResponse>
{
    public ProductSpecification(ProductFilter filter)
    {
        ProductFilterCriteria.Apply(Query, filter);

        Query.OrderByDescending(p => p.CreatedAt)
             .Select(p => new ProductResponse(p.Id, p.Name, p.Description, p.Price, p.CreatedAt));

        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }
}
