using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;

public sealed class ProductReviewByProductIdsSpecification : Specification<ProductReviewEntity, ProductReviewResponse>
{
    public ProductReviewByProductIdsSpecification(IReadOnlyCollection<Guid> productIds)
    {
        Query.Where(r => productIds.Contains(r.ProductId))
             .OrderByDescending(r => r.Audit.CreatedAtUtc)
             .Select(r => new ProductReviewResponse(r.Id, r.ProductId, r.UserId, r.Comment, r.Rating, r.Audit.CreatedAtUtc));
    }
}
