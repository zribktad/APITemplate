namespace APITemplate.Application.Features.Product.DTOs;

public sealed record ProductPriceFacetBucketResponse(
    string Label,
    decimal MinPrice,
    decimal? MaxPrice,
    int Count);
