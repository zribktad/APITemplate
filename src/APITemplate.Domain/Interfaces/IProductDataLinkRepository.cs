using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IProductDataLinkRepository
{
    Task<IReadOnlyList<ProductDataLink>> ListByProductIdAsync(
        Guid productId,
        bool includeDeleted = false,
        CancellationToken ct = default);

    Task<bool> HasActiveLinksForProductDataAsync(Guid productDataId, CancellationToken ct = default);

    Task SoftDeleteActiveLinksForProductDataAsync(Guid productDataId, CancellationToken ct = default);
}
