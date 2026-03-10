using APITemplate.Application.Common.Context;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Application.Features.ProductReview.Mappings;
using APITemplate.Application.Features.ProductReview.Specifications;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using MediatR;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview;

public sealed record GetProductReviewsQuery(ProductReviewFilter Filter) : IRequest<PagedResponse<ProductReviewResponse>>;

public sealed record GetProductReviewByIdQuery(Guid Id) : IRequest<ProductReviewResponse?>;

public sealed record GetProductReviewsByProductIdQuery(Guid ProductId) : IRequest<IReadOnlyList<ProductReviewResponse>>;

public sealed record GetProductReviewsByProductIdsQuery(IReadOnlyCollection<Guid> ProductIds)
    : IRequest<IReadOnlyDictionary<Guid, ProductReviewResponse[]>>;

public sealed record CreateProductReviewCommand(CreateProductReviewRequest Request) : IRequest<ProductReviewResponse>;

public sealed record DeleteProductReviewCommand(Guid Id) : IRequest;

public sealed class ProductReviewRequestHandlers :
    IRequestHandler<GetProductReviewsQuery, PagedResponse<ProductReviewResponse>>,
    IRequestHandler<GetProductReviewByIdQuery, ProductReviewResponse?>,
    IRequestHandler<GetProductReviewsByProductIdQuery, IReadOnlyList<ProductReviewResponse>>,
    IRequestHandler<GetProductReviewsByProductIdsQuery, IReadOnlyDictionary<Guid, ProductReviewResponse[]>>,
    IRequestHandler<CreateProductReviewCommand, ProductReviewResponse>,
    IRequestHandler<DeleteProductReviewCommand>
{
    private readonly IProductReviewRepository _reviewRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActorProvider _actorProvider;

    public ProductReviewRequestHandlers(
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

    public async Task<PagedResponse<ProductReviewResponse>> Handle(GetProductReviewsQuery request, CancellationToken ct)
    {
        var items = await _reviewRepository.ListAsync(new ProductReviewSpecification(request.Filter), ct);
        var totalCount = await _reviewRepository.CountAsync(new ProductReviewCountSpecification(request.Filter), ct);
        return new PagedResponse<ProductReviewResponse>(items, totalCount, request.Filter.PageNumber, request.Filter.PageSize);
    }

    public async Task<ProductReviewResponse?> Handle(GetProductReviewByIdQuery request, CancellationToken ct)
    {
        var item = await _reviewRepository.GetByIdAsync(request.Id, ct);
        return item?.ToResponse();
    }

    public async Task<IReadOnlyList<ProductReviewResponse>> Handle(
        GetProductReviewsByProductIdQuery request,
        CancellationToken ct)
        => await _reviewRepository.ListAsync(new ProductReviewByProductIdSpecification(request.ProductId), ct);

    public async Task<IReadOnlyDictionary<Guid, ProductReviewResponse[]>> Handle(
        GetProductReviewsByProductIdsQuery request,
        CancellationToken ct)
    {
        if (request.ProductIds.Count == 0)
            return new Dictionary<Guid, ProductReviewResponse[]>();

        var reviews = await _reviewRepository.ListAsync(new ProductReviewByProductIdsSpecification(request.ProductIds), ct);
        var lookup = reviews.ToLookup(review => review.ProductId);

        return request.ProductIds
            .Distinct()
            .ToDictionary(id => id, id => lookup[id].ToArray());
    }

    public async Task<ProductReviewResponse> Handle(CreateProductReviewCommand command, CancellationToken ct)
    {
        var userId = _actorProvider.ActorId;
        var productExists = await _productRepository.GetByIdAsync(command.Request.ProductId, ct) is not null;

        if (!productExists)
        {
            throw new NotFoundException(
                "Product",
                command.Request.ProductId,
                ErrorCatalog.Reviews.ProductNotFoundForReview);
        }

        var review = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var entity = new ProductReviewEntity
            {
                Id = Guid.NewGuid(),
                ProductId = command.Request.ProductId,
                UserId = userId,
                Comment = command.Request.Comment,
                Rating = command.Request.Rating
            };

            await _reviewRepository.AddAsync(entity, ct);
            return entity;
        }, ct);

        return review.ToResponse();
    }

    public async Task Handle(DeleteProductReviewCommand command, CancellationToken ct)
    {
        var userId = _actorProvider.ActorId;
        var review = await _reviewRepository.GetByIdAsync(command.Id, ct)
            ?? throw new NotFoundException("ProductReview", command.Id, ErrorCatalog.Reviews.ReviewNotFound);

        if (review.UserId != userId)
        {
            throw new ForbiddenException(
                "You can only delete your own reviews.",
                ErrorCatalog.Auth.Forbidden);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _reviewRepository.DeleteAsync(command.Id, ct, ErrorCatalog.Reviews.ReviewNotFound);
        }, ct);
    }
}
