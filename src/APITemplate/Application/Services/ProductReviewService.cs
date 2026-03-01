using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;
using APITemplate.Application.Mappings;
using APITemplate.Application.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Application.Services;

public sealed class ProductReviewService : IProductReviewService
{
    private readonly IProductReviewRepository _reviewRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductReviewService(
        IProductReviewRepository reviewRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ProductReviewResponse>> GetAllAsync(ProductReviewFilter filter, CancellationToken ct = default)
    {
        return await _reviewRepository.AsQueryable()
            .Where(new ProductReviewSpecification(filter).Criteria)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ProductReviewResponse(r.Id, r.ProductId, r.ReviewerName, r.Comment, r.Rating, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductReviewResponse>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
    {
        return await _reviewRepository.AsQueryable()
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ProductReviewResponse(r.Id, r.ProductId, r.ReviewerName, r.Comment, r.Rating, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ProductReviewResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var review = await _reviewRepository.GetByIdAsync(id, ct);
        return review?.ToResponse();
    }

    public async Task<ProductReviewResponse> CreateAsync(CreateProductReviewRequest request, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

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
        await _unitOfWork.CommitAsync(ct);
        return review.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _reviewRepository.DeleteAsync(id, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
