namespace APITemplate.Application.Features.Product.DTOs;

public sealed record ProductCategoryFacetValue(
    Guid? CategoryId,
    string CategoryName,
    int Count);
