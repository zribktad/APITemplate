namespace APITemplate.Api.GraphQL.Models;

public sealed record ProductPageResult(
    PagedResponse<ProductResponse> Page,
    ProductSearchFacetsResponse Facets)
    : IPagedItems<ProductResponse>, IHasFacets<ProductSearchFacetsResponse>;
