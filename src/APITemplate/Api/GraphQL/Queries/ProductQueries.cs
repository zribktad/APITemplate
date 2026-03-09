using APITemplate.Api.GraphQL.Models;
using APITemplate.Application.Common.Validation;
using FluentValidation;
using HotChocolate.Authorization;

namespace APITemplate.Api.GraphQL.Queries;

[Authorize]
public class ProductQueries
{
    public async Task<ProductPageResult> GetProducts(
        ProductQueryInput? input,
        [Service] IProductService productService,
        [Service] IValidator<ProductFilter> validator,
        CancellationToken ct)
    {
        var filter = new ProductFilter(
            input?.Name,
            input?.Description,
            input?.MinPrice,
            input?.MaxPrice,
            input?.CreatedFrom,
            input?.CreatedTo,
            input?.SortBy,
            input?.SortDirection,
            input?.PageNumber ?? 1,
            input?.PageSize ?? PaginationFilter.DefaultPageSize,
            input?.Query,
            input?.CategoryIds);

        await validator.ValidateAndThrowAppAsync(filter, ct);

        var page = await productService.GetAllAsync(filter, ct);
        return new ProductPageResult(
            page.Page,
            page.Facets);
    }

    public async Task<ProductResponse?> GetProductById(
        Guid id,
        [Service] IProductService productService,
        CancellationToken ct)
        => await productService.GetByIdAsync(id, ct);
}
