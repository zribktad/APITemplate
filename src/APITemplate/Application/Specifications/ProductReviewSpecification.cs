using System.Linq.Expressions;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class ProductReviewSpecification : ISpecification<ProductReview>
{
    private readonly ProductReviewFilter _filter;

    public ProductReviewSpecification(ProductReviewFilter filter)
    {
        _filter = filter;
    }

    public Expression<Func<ProductReview, bool>> Criteria => r =>
        (!_filter.ProductId.HasValue                       || r.ProductId == _filter.ProductId.Value) &&
        (string.IsNullOrWhiteSpace(_filter.ReviewerName)   || r.ReviewerName.Contains(_filter.ReviewerName)) &&
        (!_filter.MinRating.HasValue    || r.Rating >= _filter.MinRating.Value) &&
        (!_filter.MaxRating.HasValue    || r.Rating <= _filter.MaxRating.Value) &&
        (!_filter.CreatedFrom.HasValue  || r.CreatedAt >= _filter.CreatedFrom.Value) &&
        (!_filter.CreatedTo.HasValue    || r.CreatedAt <= _filter.CreatedTo.Value);
}
