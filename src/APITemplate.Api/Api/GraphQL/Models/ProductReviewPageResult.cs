namespace APITemplate.Api.GraphQL.Models;

public sealed record ProductReviewPageResult(
    PagedResponse<ProductReviewResponse> Page)
    : IPagedItems<ProductReviewResponse>;
