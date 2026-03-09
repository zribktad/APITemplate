using APITemplate.Application.Common.Context;
using APITemplate.Application.Features.ProductReview.Mappings;
using APITemplate.Application.Features.ProductReview.Specifications;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.ProductReview.Services;
public sealed class ProductReviewService : IProductReviewService
{
    private readonly IProductReviewRepository _reviewRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActorProvider _actorProvider;

    public ProductReviewService(
        IProductReviewRepository reviewRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        IActorProvider actorProvider)
    {
        _reviewRepository = reviewRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _actorProvider = actorProvider;
    }

    public async Task<PagedResponse<ProductReviewResponse>> GetAllAsync(ProductReviewFilter filter, CancellationToken ct = default)
    {
        var items = await _reviewRepository.ListAsync(new ProductReviewSpecification(filter), ct);
        var totalCount = await _reviewRepository.CountAsync(new ProductReviewCountSpecification(filter), ct);
        return new PagedResponse<ProductReviewResponse>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<ProductReviewResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _reviewRepository.GetByIdAsync(id, ct);
        return item?.ToResponse();
    }

    public async Task<IReadOnlyList<ProductReviewResponse>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        => await _reviewRepository.ListAsync(new ProductReviewByProductIdSpecification(productId), ct);

    public async Task<IReadOnlyDictionary<Guid, ProductReviewResponse[]>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, ProductReviewResponse[]>();

        var reviews = await _reviewRepository.ListAsync(new ProductReviewByProductIdsSpecification(productIds), ct);
        var lookup = reviews.ToLookup(r => r.ProductId);

        return productIds
            .Distinct()
            .ToDictionary(id => id, id => lookup[id].ToArray());
    }

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
