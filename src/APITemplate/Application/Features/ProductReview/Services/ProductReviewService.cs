using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.ProductReview.Services;
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

    public async Task<PagedResponse<ProductReviewResponse>> GetAllAsync(ProductReviewFilter filter, CancellationToken ct = default)
    {
        var items = await _reviewRepository.ListAsync(new ProductReviewSpecification(filter), ct);
        var totalCount = await _reviewRepository.CountAsync(new ProductReviewCountSpecification(filter), ct);

        return new PagedResponse<ProductReviewResponse>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<IReadOnlyList<ProductReviewResponse>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        => await _reviewRepository.ListAsync(new ProductReviewByProductIdSpecification(productId), ct);

    public async Task<ProductReviewResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var review = await _reviewRepository.GetByIdAsync(id, ct);
        return review?.ToResponse();
    }

    public async Task<ProductReviewResponse> CreateAsync(CreateProductReviewRequest request, CancellationToken ct = default)
    {
        ProductReviewEntity review = null!;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var productExists = await _productRepository.GetByIdAsync(request.ProductId, ct) is not null;
            if (!productExists)
                throw new NotFoundException(nameof(Product), request.ProductId, ErrorCatalog.Reviews.ProductNotFoundForReview);

            review = new ProductReviewEntity
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                ReviewerName = request.ReviewerName,
                Comment = request.Comment,
                Rating = request.Rating,
                CreatedAt = DateTime.UtcNow
            };

            await _reviewRepository.AddAsync(review, ct);
            await _unitOfWork.CommitAsync(ct);
        }, ct);

        return review.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _reviewRepository.DeleteAsync(id, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
