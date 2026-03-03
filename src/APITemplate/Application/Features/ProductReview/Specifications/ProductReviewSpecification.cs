using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;
public sealed class ProductReviewSpecification : Specification<ProductReviewEntity, ProductReviewResponse>
{
    public ProductReviewSpecification(ProductReviewFilter filter)
    {
        ProductReviewFilterCriteria.Apply(Query, filter);

        ApplySorting(Query, filter);

        Query.Select(r => new ProductReviewResponse(r.Id, r.ProductId, r.ReviewerName, r.Comment, r.Rating, r.CreatedAt));

        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }

    private static void ApplySorting(ISpecificationBuilder<ProductReviewEntity> query, ProductReviewFilter filter)
    {
        var sortBy = filter.SortBy?.Trim().ToLowerInvariant();
        var desc = !string.Equals(filter.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        switch (sortBy)
        {
            case "rating":
                if (desc) query.OrderByDescending(r => r.Rating);
                else query.OrderBy(r => r.Rating);
                break;
            case "reviewername":
            case "reviewer_name":
            case "reviewer":
                if (desc) query.OrderByDescending(r => r.ReviewerName);
                else query.OrderBy(r => r.ReviewerName);
                break;
            default:
                if (desc) query.OrderByDescending(r => r.CreatedAt);
                else query.OrderBy(r => r.CreatedAt);
                break;
        }
    }
}
