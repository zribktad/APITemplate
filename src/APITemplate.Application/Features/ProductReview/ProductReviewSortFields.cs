using APITemplate.Application.Common.Sorting;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview;
public static class ProductReviewSortFields
{
    public static readonly SortField Rating = new("rating");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<ProductReviewEntity> Map = new SortFieldMap<ProductReviewEntity>()
        .Add(Rating, r => (object)r.Rating)
        .Add(CreatedAt, r => r.Audit.CreatedAtUtc)
        .Default(r => r.Audit.CreatedAtUtc);
}
