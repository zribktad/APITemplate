using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;
public sealed class ProductReviewSpecification : Specification<ProductReviewEntity, ProductReviewResponse>
{
    public ProductReviewSpecification(ProductReviewFilter filter)
    {
        ProductReviewFilterCriteria.Apply(Query, filter);

        Query.OrderByDescending(r => r.CreatedAt)
             .Select(r => new ProductReviewResponse(r.Id, r.ProductId, r.ReviewerName, r.Comment, r.Rating, r.CreatedAt));

        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }
}
