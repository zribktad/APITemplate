using MediatR;

namespace APITemplate.Application.Common.Events;

public sealed record ProductsChangedNotification : INotification;

public sealed record CategoriesChangedNotification : INotification;

public sealed record ProductReviewsChangedNotification : INotification;
