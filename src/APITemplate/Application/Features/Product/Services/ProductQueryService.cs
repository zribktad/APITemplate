using APITemplate.Application.Features.Product.Mappings;
using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Product.Services;

public sealed class ProductQueryService : IProductQueryService
{
    private readonly IProductRepository _repository;

    public ProductQueryService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<ProductResponse>> GetPagedAsync(ProductFilter filter, CancellationToken ct = default)
    {
        var items = await _repository.ListAsync(new ProductSpecification(filter), ct);
        var totalCount = await _repository.CountAsync(new ProductCountSpecification(filter), ct);
        return new PagedResponse<ProductResponse>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _repository.FirstOrDefaultAsync(new ProductByIdSpecification(id), ct);
    }
}
