using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;
public sealed class ProductReviewSpecification : Specification<ProductReviewEntity, ProductReviewResponse>
{
    public ProductReviewSpecification(ProductReviewFilter filter)
    {
        ProductReviewFilterCriteria.Apply(Query, filter);

        ProductReviewSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);

        Query.Select(r => new ProductReviewResponse(r.Id, r.ProductId, r.UserId, r.Comment, r.Rating, r.Audit.CreatedAtUtc));

        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }
}
