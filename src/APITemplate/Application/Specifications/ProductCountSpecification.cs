using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class ProductCountSpecification : Specification<Product>
{
    public ProductCountSpecification(ProductFilter filter)
    {
        ProductFilterCriteria.Apply(Query, filter);
    }
}