namespace APITemplate.Api.GraphQL.Models;

public sealed record CategoryPageResult(
    PagedResponse<CategoryResponse> Page)
    : IPagedItems<CategoryResponse>;
