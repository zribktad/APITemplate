using APITemplate.Application.Common.Context;
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
    private readonly IActorProvider _actorProvider;

    public ProductReviewService(
        IProductReviewRepository reviewRepository,
        IProductReviewQueryService queryService,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        IActorProvider actorProvider)
    {
        _reviewRepository = reviewRepository;
        _queryService = queryService;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _actorProvider = actorProvider;
    }

    public Task<PagedResponse<ProductReviewResponse>> GetAllAsync(ProductReviewFilter filter, CancellationToken ct = default)
        => _queryService.GetPagedAsync(filter, ct);

    public Task<IReadOnlyList<ProductReviewResponse>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        => _queryService.GetByProductIdAsync(productId, ct);

    public Task<ProductReviewResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _queryService.GetByIdAsync(id, ct);

    public async Task<ProductReviewResponse> CreateAsync(CreateProductReviewRequest request, CancellationToken ct = default)
    {
        var userId = _actorProvider.ActorId;

        var productExists = await _productRepository.GetByIdAsync(request.ProductId, ct) is not null;
        if (!productExists)
            throw new NotFoundException(
                "Product",
                request.ProductId,
                ErrorCatalog.Reviews.ProductNotFoundForReview);

        var review = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var entity = new ProductReviewEntity
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                UserId = userId,
                Comment = request.Comment,
                Rating = request.Rating
            };

            await _reviewRepository.AddAsync(entity, ct);
            return entity;
        }, ct);

        return review.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var userId = _actorProvider.ActorId;

        var review = await _reviewRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("ProductReview", id, ErrorCatalog.Reviews.ReviewNotFound);

        if (review.UserId != userId)
            throw new ForbiddenException(
                "You can only delete your own reviews.",
                ErrorCatalog.Auth.Forbidden);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _reviewRepository.DeleteAsync(id, ct, ErrorCatalog.Reviews.ReviewNotFound);
        }, ct);
    }
}
