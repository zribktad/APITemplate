using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Repositories;

public interface IProductRepository : IRepository<ProductEntity>
{
    Task<IReadOnlyList<ProductResponse>> ListAsync(ProductFilter filter, CancellationToken ct = default);
    Task<int> CountAsync(ProductFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ProductCategoryFacetValue>> GetCategoryFacetsAsync(ProductFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ProductPriceFacetBucketResponse>> GetPriceFacetsAsync(ProductFilter filter, CancellationToken ct = default);
}
