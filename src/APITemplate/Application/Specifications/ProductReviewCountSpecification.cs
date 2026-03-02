using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class ProductReviewCountSpecification : Specification<ProductReview>
{
    public ProductReviewCountSpecification(ProductReviewFilter filter)
    {
        if (filter.ProductId.HasValue)
            Query.Where(r => r.ProductId == filter.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(filter.ReviewerName))
            Query.Where(r => r.ReviewerName.Contains(filter.ReviewerName));

        if (filter.MinRating.HasValue)
            Query.Where(r => r.Rating >= filter.MinRating.Value);

        if (filter.MaxRating.HasValue)
            Query.Where(r => r.Rating <= filter.MaxRating.Value);

        if (filter.CreatedFrom.HasValue)
            Query.Where(r => r.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            Query.Where(r => r.CreatedAt <= filter.CreatedTo.Value);
    }
}