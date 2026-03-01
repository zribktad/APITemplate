using System.Linq.Expressions;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class ProductSpecification : ISpecification<Product>
{
    private readonly ProductFilter _filter;

    public ProductSpecification(ProductFilter filter)
    {
        _filter = filter;
    }

    public Expression<Func<Product, bool>> Criteria => p =>
        (string.IsNullOrWhiteSpace(_filter.Name)        || p.Name.Contains(_filter.Name)) &&
        (string.IsNullOrWhiteSpace(_filter.Description) || (p.Description != null && p.Description.Contains(_filter.Description))) &&
        (!_filter.MinPrice.HasValue    || p.Price >= _filter.MinPrice.Value) &&
        (!_filter.MaxPrice.HasValue    || p.Price <= _filter.MaxPrice.Value) &&
        (!_filter.CreatedFrom.HasValue || p.CreatedAt >= _filter.CreatedFrom.Value) &&
        (!_filter.CreatedTo.HasValue   || p.CreatedAt <= _filter.CreatedTo.Value);
}
