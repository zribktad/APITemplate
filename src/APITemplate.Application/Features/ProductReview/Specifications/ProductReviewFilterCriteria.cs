using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;
internal static class ProductReviewFilterCriteria
{
    internal static void Apply(ISpecificationBuilder<ProductReviewEntity> query, ProductReviewFilter filter)
    {
        if (filter.ProductId.HasValue)
            query.Where(r => r.ProductId == filter.ProductId.Value);

        if (filter.UserId.HasValue)
            query.Where(r => r.UserId == filter.UserId.Value);

        if (filter.MinRating.HasValue)
            query.Where(r => r.Rating >= filter.MinRating.Value);

        if (filter.MaxRating.HasValue)
            query.Where(r => r.Rating <= filter.MaxRating.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(r => r.Audit.CreatedAtUtc >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(r => r.Audit.CreatedAtUtc <= filter.CreatedTo.Value);
    }
}
