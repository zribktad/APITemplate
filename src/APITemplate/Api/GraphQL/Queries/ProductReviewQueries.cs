using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Api.GraphQL.Queries;

[ExtendObjectType(typeof(ProductQueries))]
public class ProductReviewQueries
{
    [UsePaging(MaxPageSize = 100, DefaultPageSize = 20)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ProductReview> GetReviews([Service] IProductReviewRepository repo)
        => repo.AsQueryable();

    [UseFirstOrDefault]
    [UseProjection]
    public IQueryable<ProductReview> GetReviewById(
        Guid id,
        [Service] IProductReviewRepository repo)
        => repo.AsQueryable().Where(r => r.Id == id);

    [UsePaging(MaxPageSize = 100, DefaultPageSize = 20)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ProductReview> GetReviewsByProductId(
        Guid productId,
        [Service] IProductReviewRepository repo)
        => repo.AsQueryable().Where(r => r.ProductId == productId);
}
