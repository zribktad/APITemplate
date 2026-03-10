using APITemplate.Api.GraphQL.Models;
using HotChocolate.Authorization;
using MediatR;

namespace APITemplate.Api.GraphQL.Queries;

[Authorize]
[ExtendObjectType(typeof(ProductQueries))]
public class ProductReviewQueries
{
    public async Task<ProductReviewPageResult> GetReviews(
        ProductReviewQueryInput? input,
        [Service] ISender sender,
        CancellationToken ct)
    {
        var filter = new ProductReviewFilter(
            input?.ProductId,
            input?.UserId,
            input?.MinRating,
            input?.MaxRating,
            input?.CreatedFrom,
            input?.CreatedTo,
            input?.SortBy,
            input?.SortDirection,
            input?.PageNumber ?? 1,
            input?.PageSize ?? 20);

        var page = await sender.Send(new GetProductReviewsQuery(filter), ct);
        return new ProductReviewPageResult(page);
    }

    public async Task<ProductReviewResponse?> GetReviewById(
        Guid id,
        [Service] ISender sender,
        CancellationToken ct)
        => await sender.Send(new GetProductReviewByIdQuery(id), ct);

    public async Task<ProductReviewPageResult> GetReviewsByProductId(
        Guid productId,
        int pageNumber,
        int pageSize,
        [Service] ISender sender,
        CancellationToken ct)
    {
        var filter = new ProductReviewFilter(ProductId: productId, PageNumber: pageNumber, PageSize: pageSize);
        var page = await sender.Send(new GetProductReviewsQuery(filter), ct);
        return new ProductReviewPageResult(page);
    }
}
