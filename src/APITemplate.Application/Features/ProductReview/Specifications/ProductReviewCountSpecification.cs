using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;
public sealed class ProductReviewCountSpecification : Specification<ProductReviewEntity>
{
    public ProductReviewCountSpecification(ProductReviewFilter filter)
    {
        ProductReviewFilterCriteria.Apply(Query, filter);
    }
}
