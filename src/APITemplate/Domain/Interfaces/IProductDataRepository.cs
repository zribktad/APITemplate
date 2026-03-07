using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IProductDataRepository
{
    Task<ProductData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<ProductData>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<List<ProductData>> GetAllAsync(string? type = null, CancellationToken ct = default);

    Task<ProductData> CreateAsync(ProductData productData, CancellationToken ct = default);

    Task SoftDeleteAsync(Guid id, Guid actorId, DateTime deletedAtUtc, CancellationToken ct = default);
}
