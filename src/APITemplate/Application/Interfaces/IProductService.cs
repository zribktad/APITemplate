using APITemplate.Application.DTOs;

namespace APITemplate.Application.Interfaces;

public interface IProductService
{
    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ProductResponse>> GetAllAsync(CancellationToken ct = default);

    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
