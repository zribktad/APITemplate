using APITemplate.Application.DTOs;

namespace APITemplate.Application.Interfaces;

public interface IProductService
{
    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResponse<ProductResponse>> GetAllAsync(ProductFilter filter, CancellationToken ct = default);

    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
