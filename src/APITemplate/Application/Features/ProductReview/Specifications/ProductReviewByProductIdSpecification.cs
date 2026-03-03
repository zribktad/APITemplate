using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;
public sealed class ProductReviewByProductIdSpecification : Specification<ProductReviewEntity, ProductReviewResponse>
{
    public ProductReviewByProductIdSpecification(Guid productId)
    {
        Query.Where(r => r.ProductId == productId)
             .OrderByDescending(r => r.CreatedAt)
             .Select(r => new ProductReviewResponse(r.Id, r.ProductId, r.ReviewerName, r.Comment, r.Rating, r.CreatedAt));
    }
}
