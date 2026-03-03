using APITemplate.Application.Features.ProductReview.Mappings;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.ProductReview.Services;
public sealed class ProductReviewService : IProductReviewService
{
    private readonly IProductReviewRepository _reviewRepository;
    private readonly IProductReviewQueryService _queryService;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductReviewService(
        IProductReviewRepository reviewRepository,
        IProductReviewQueryService queryService,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository;
        _queryService = queryService;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResponse<ProductReviewResponse>> GetAllAsync(ProductReviewFilter filter, CancellationToken ct = default)
        => _queryService.GetPagedAsync(filter, ct);

    public Task<IReadOnlyList<ProductReviewResponse>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        => _queryService.GetByProductIdAsync(productId, ct);

    public Task<ProductReviewResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _queryService.GetByIdAsync(id, ct);

    public async Task<ProductReviewResponse> CreateAsync(CreateProductReviewRequest request, CancellationToken ct = default)
    {
        ProductReviewEntity review = null!;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var productExists = await _productRepository.GetByIdAsync(request.ProductId, ct) is not null;
            if (!productExists)
                throw new NotFoundException(
                    "Product",
                    request.ProductId,
                    ErrorCatalog.Reviews.ProductNotFoundForReview);

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
