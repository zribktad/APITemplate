using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

internal static class ProductReviewFilterCriteria
{
    internal static void Apply(ISpecificationBuilder<ProductReview> query, ProductReviewFilter filter)
    {
        if (filter.ProductId.HasValue)
            query.Where(r => r.ProductId == filter.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(filter.ReviewerName))
            query.Where(r => r.ReviewerName.Contains(filter.ReviewerName));

        if (filter.MinRating.HasValue)
            query.Where(r => r.Rating >= filter.MinRating.Value);

        if (filter.MaxRating.HasValue)
            query.Where(r => r.Rating <= filter.MaxRating.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(r => r.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(r => r.CreatedAt <= filter.CreatedTo.Value);
    }
}
