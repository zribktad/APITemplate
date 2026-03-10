using APITemplate.Application.Features.ProductReview.Mappings;
using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;
public sealed class ProductReviewByProductIdSpecification : Specification<ProductReviewEntity, ProductReviewResponse>
{
    public ProductReviewByProductIdSpecification(Guid productId)
    {
        Query.Where(r => r.ProductId == productId)
             .OrderByDescending(r => r.Audit.CreatedAtUtc)
             .Select(ProductReviewMappings.Projection);
    }
}
