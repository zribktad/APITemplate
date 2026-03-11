using APITemplate.Application.Common.Events;
using MediatR;

namespace APITemplate.Api.Cache;

public sealed class CacheInvalidationNotificationHandler :
    INotificationHandler<ProductsChangedNotification>,
    INotificationHandler<CategoriesChangedNotification>,
    INotificationHandler<ProductReviewsChangedNotification>
{
    private readonly IOutputCacheInvalidationService _outputCacheInvalidationService;

    public CacheInvalidationNotificationHandler(IOutputCacheInvalidationService outputCacheInvalidationService)
    {
        _outputCacheInvalidationService = outputCacheInvalidationService;
    }

    public Task Handle(ProductsChangedNotification notification, CancellationToken cancellationToken)
        => _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Products, cancellationToken);

    public Task Handle(CategoriesChangedNotification notification, CancellationToken cancellationToken)
        => _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Categories, cancellationToken);

    public Task Handle(ProductReviewsChangedNotification notification, CancellationToken cancellationToken)
        => _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Reviews, cancellationToken);
}
