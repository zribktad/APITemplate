using APITemplate.Api.GraphQL.Models;
using APITemplate.Application.Common.Validation;
using FluentValidation;
using HotChocolate.Authorization;

namespace APITemplate.Api.GraphQL.Queries;

[Authorize]
[ExtendObjectType(typeof(ProductQueries))]
public class ProductReviewQueries
{
    public async Task<ProductReviewPageResult> GetReviews(
        ProductReviewQueryInput? input,
        [Service] IProductReviewQueryService queryService,
        [Service] IValidator<ProductReviewFilter> validator,
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

        await validator.ValidateAndThrowAppAsync(filter, ct);

        var page = await queryService.GetPagedAsync(filter, ct);
        return new ProductReviewPageResult(page.Items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    public async Task<ProductReviewResponse?> GetReviewById(
        Guid id,
        [Service] IProductReviewQueryService queryService,
        CancellationToken ct)
        => await queryService.GetByIdAsync(id, ct);

    public async Task<ProductReviewPageResult> GetReviewsByProductId(
        Guid productId,
        int pageNumber,
        int pageSize,
        [Service] IProductReviewQueryService queryService,
        [Service] IValidator<ProductReviewFilter> validator,
        CancellationToken ct)
    {
        var filter = new ProductReviewFilter(ProductId: productId, PageNumber: pageNumber, PageSize: pageSize);

        await validator.ValidateAndThrowAppAsync(filter, ct);

        var page = await queryService.GetPagedAsync(filter, ct);
        return new ProductReviewPageResult(page.Items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
