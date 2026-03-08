namespace APITemplate.Application.Features.Product.Interfaces;

public interface IProductReadRepository
{
    Task<IReadOnlyList<ProductResponse>> ListAsync(ProductFilter filter, CancellationToken ct = default);
    Task<int> CountAsync(ProductFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ProductCategoryFacetValue>> GetCategoryFacetsAsync(ProductFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ProductPriceFacetBucketResponse>> GetPriceFacetsAsync(ProductFilter filter, CancellationToken ct = default);
}
