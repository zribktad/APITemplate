using APITemplate.Application.Common.Events;
using MediatR;

namespace APITemplate.Api.Cache;

public sealed class CacheInvalidationNotificationHandler
    : INotificationHandler<ProductsChangedNotification>,
        INotificationHandler<CategoriesChangedNotification>,
        INotificationHandler<ProductReviewsChangedNotification>,
        INotificationHandler<ProductDataChangedNotification>,
        INotificationHandler<TenantsChangedNotification>,
        INotificationHandler<TenantInvitationsChangedNotification>,
        INotificationHandler<UsersChangedNotification>
{
    private readonly IOutputCacheInvalidationService _outputCacheInvalidationService;

    public CacheInvalidationNotificationHandler(
        IOutputCacheInvalidationService outputCacheInvalidationService
    )
    {
        _outputCacheInvalidationService = outputCacheInvalidationService;
    }

    public Task Handle(
        ProductsChangedNotification notification,
        CancellationToken cancellationToken
    ) => _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Products, cancellationToken);

    public Task Handle(
        CategoriesChangedNotification notification,
        CancellationToken cancellationToken
    ) => _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Categories, cancellationToken);

    public Task Handle(
        ProductReviewsChangedNotification notification,
        CancellationToken cancellationToken
    ) => _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Reviews, cancellationToken);

    public Task Handle(
        ProductDataChangedNotification notification,
        CancellationToken cancellationToken
    ) =>
        _outputCacheInvalidationService.EvictAsync(CachePolicyNames.ProductData, cancellationToken);

    public Task Handle(
        TenantsChangedNotification notification,
        CancellationToken cancellationToken
    ) => _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Tenants, cancellationToken);

    public Task Handle(
        TenantInvitationsChangedNotification notification,
        CancellationToken cancellationToken
    ) =>
        _outputCacheInvalidationService.EvictAsync(
            CachePolicyNames.TenantInvitations,
            cancellationToken
        );

    public Task Handle(
        UsersChangedNotification notification,
        CancellationToken cancellationToken
    ) => _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Users, cancellationToken);
}
