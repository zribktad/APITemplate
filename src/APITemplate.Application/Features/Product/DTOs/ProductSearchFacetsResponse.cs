namespace APITemplate.Application.Features.Product.DTOs;

public sealed record ProductSearchFacetsResponse(
    IReadOnlyCollection<ProductCategoryFacetValue> Categories,
    IReadOnlyCollection<ProductPriceFacetBucketResponse> PriceBuckets);
