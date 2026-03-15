using MediatR;

namespace APITemplate.Application.Common.Events;

public sealed record ProductsChangedNotification : INotification;

public sealed record CategoriesChangedNotification : INotification;

public sealed record ProductReviewsChangedNotification : INotification;

public sealed record ProductDataChangedNotification : INotification;

public sealed record TenantsChangedNotification : INotification;

public sealed record TenantInvitationsChangedNotification : INotification;

public sealed record UsersChangedNotification : INotification;
