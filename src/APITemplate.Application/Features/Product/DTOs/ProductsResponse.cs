namespace APITemplate.Application.Features.Product.DTOs;

public sealed record ProductsResponse(
    PagedResponse<ProductResponse> Page,
    ProductSearchFacetsResponse Facets)
    : IPagedItems<ProductResponse>, IHasFacets<ProductSearchFacetsResponse>;
