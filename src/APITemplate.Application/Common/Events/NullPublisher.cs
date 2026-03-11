using MediatR;

namespace APITemplate.Application.Common.Events;

internal sealed class NullPublisher : IPublisher
{
    public static IPublisher Instance { get; } = new NullPublisher();

    private NullPublisher() { }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => Task.CompletedTask;
}
