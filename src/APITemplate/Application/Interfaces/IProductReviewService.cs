using APITemplate.Application.DTOs;

namespace APITemplate.Application.Interfaces;

public interface IProductReviewService
{
    Task<PagedResponse<ProductReviewResponse>> GetAllAsync(ProductReviewFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ProductReviewResponse>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<ProductReviewResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductReviewResponse> CreateAsync(CreateProductReviewRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
