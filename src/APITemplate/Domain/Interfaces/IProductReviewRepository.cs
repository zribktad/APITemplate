using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IProductReviewRepository : IRepository<ProductReview>
{
    Task<IReadOnlyList<ProductReview>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
}
