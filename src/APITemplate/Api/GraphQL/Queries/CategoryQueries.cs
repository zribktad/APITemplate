using APITemplate.Api.GraphQL.Models;
using APITemplate.Application.Common.Validation;
using FluentValidation;
using HotChocolate.Authorization;

namespace APITemplate.Api.GraphQL.Queries;

[Authorize]
[ExtendObjectType(typeof(ProductQueries))]
public sealed class CategoryQueries
{
    public async Task<CategoryPageResult> GetCategories(
        CategoryQueryInput? input,
        [Service] ICategoryQueryService queryService,
        [Service] IValidator<CategoryFilter> validator,
        CancellationToken ct)
    {
        var filter = new CategoryFilter(
            input?.Query,
            input?.SortBy,
            input?.SortDirection,
            input?.PageNumber ?? 1,
            input?.PageSize ?? PaginationFilter.DefaultPageSize);

        await validator.ValidateAndThrowAppAsync(filter, ct);

        var page = await queryService.GetPagedAsync(filter, ct);
        return new CategoryPageResult(page);
    }

    public async Task<CategoryResponse?> GetCategoryById(
        Guid id,
        [Service] ICategoryQueryService queryService,
        CancellationToken ct)
        => await queryService.GetByIdAsync(id, ct);
}
