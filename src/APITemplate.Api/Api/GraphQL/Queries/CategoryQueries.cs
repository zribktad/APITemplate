using APITemplate.Api.GraphQL.Models;
using HotChocolate.Authorization;
using MediatR;

namespace APITemplate.Api.GraphQL.Queries;

[Authorize]
[ExtendObjectType(typeof(ProductQueries))]
public sealed class CategoryQueries
{
    public async Task<CategoryPageResult> GetCategories(
        CategoryQueryInput? input,
        [Service] ISender sender,
        CancellationToken ct)
    {
        var filter = new CategoryFilter(
            input?.Query,
            input?.SortBy,
            input?.SortDirection,
            input?.PageNumber ?? 1,
            input?.PageSize ?? PaginationFilter.DefaultPageSize);

        var page = await sender.Send(new GetCategoriesQuery(filter), ct);
        return new CategoryPageResult(page);
    }

    public async Task<CategoryResponse?> GetCategoryById(
        Guid id,
        [Service] ISender sender,
        CancellationToken ct)
        => await sender.Send(new GetCategoryByIdQuery(id), ct);
}
