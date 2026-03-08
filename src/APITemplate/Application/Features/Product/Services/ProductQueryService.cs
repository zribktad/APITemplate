using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Product.Services;

public sealed class ProductQueryService : IProductQueryService
{
    private readonly IProductReadRepository _readRepository;
    private readonly IProductRepository _productRepository;

    public ProductQueryService(IProductReadRepository readRepository, IProductRepository productRepository)
    {
        _readRepository = readRepository;
        _productRepository = productRepository;
    }

    public async Task<ProductsResponse> GetPagedAsync(ProductFilter filter, CancellationToken ct = default)
    {
        var itemsTask = _readRepository.ListAsync(filter, ct);
        var totalCountTask = _readRepository.CountAsync(filter, ct);
        var categoryFacetsTask = _readRepository.GetCategoryFacetsAsync(filter, ct);
        var priceFacetsTask = _readRepository.GetPriceFacetsAsync(filter, ct);

        await Task.WhenAll(itemsTask, totalCountTask, categoryFacetsTask, priceFacetsTask);

        return new ProductsResponse(
            new PagedResponse<ProductResponse>(itemsTask.Result, totalCountTask.Result, filter.PageNumber, filter.PageSize),
            new ProductSearchFacetsResponse(categoryFacetsTask.Result, priceFacetsTask.Result));
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _productRepository.FirstOrDefaultAsync(new ProductByIdSpecification(id), ct);
    }
}
