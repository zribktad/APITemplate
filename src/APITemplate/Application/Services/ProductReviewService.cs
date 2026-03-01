using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace APITemplate.Application.Services;

public sealed class ProductReviewService : IProductReviewService
{
    private readonly IProductReviewRepository _reviewRepository;
    private readonly IProductRepository _productRepository;

    public ProductReviewService(IProductReviewRepository reviewRepository, IProductRepository productRepository)
    {
        _reviewRepository = reviewRepository;
        _productRepository = productRepository;
    }

    public async Task<IReadOnlyList<ProductReviewResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var reviews = await _reviewRepository.GetAllAsync(ct);
        return reviews.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<ProductReviewResponse>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
    {
        var reviews = await _reviewRepository.GetByProductIdAsync(productId, ct);
        return reviews.Select(MapToResponse).ToList();
    }

    public async Task<ProductReviewResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var review = await _reviewRepository.GetByIdAsync(id, ct);
        return review is null ? null : MapToResponse(review);
    }

    public async Task<ProductReviewResponse> CreateAsync(CreateProductReviewRequest request, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, ct)
            ?? throw new KeyNotFoundException($"Product with id '{request.ProductId}' not found.");

        var review = new ProductReview
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            ReviewerName = request.ReviewerName,
            Comment = request.Comment,
            Rating = request.Rating,
            CreatedAt = DateTime.UtcNow
        };

        await _reviewRepository.AddAsync(review, ct);
        return MapToResponse(review);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _reviewRepository.DeleteAsync(id, ct);
    }

    private static ProductReviewResponse MapToResponse(ProductReview review) =>
        new(review.Id, review.ProductId, review.ReviewerName, review.Comment, review.Rating, review.CreatedAt);
}
