using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace APITemplate.Application.Common.Events;

public sealed class EmailNotificationHandler
    : INotificationHandler<UserRegisteredNotification>,
        INotificationHandler<TenantInvitationCreatedNotification>,
        INotificationHandler<UserRoleChangedNotification>
{
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IEmailQueue _emailQueue;
    private readonly EmailOptions _options;

    public EmailNotificationHandler(
        IEmailTemplateRenderer templateRenderer,
        IEmailQueue emailQueue,
        IOptions<EmailOptions> options
    )
    {
        _templateRenderer = templateRenderer;
        _emailQueue = emailQueue;
        _options = options.Value;
    }

    public async Task Handle(UserRegisteredNotification notification, CancellationToken ct)
    {
        var html = await _templateRenderer.RenderAsync(
            EmailTemplateNames.UserRegistration,
            new
            {
                notification.Username,
                notification.Email,
                LoginUrl = $"{_options.BaseUrl}/login",
            },
            ct
        );

        await _emailQueue.EnqueueAsync(
            new EmailMessage(notification.Email, "Welcome to the platform!", html),
            ct
        );
    }

    public async Task Handle(TenantInvitationCreatedNotification notification, CancellationToken ct)
    {
        var html = await _templateRenderer.RenderAsync(
            EmailTemplateNames.TenantInvitation,
            new
            {
                notification.Email,
                notification.TenantName,
                InvitationUrl = $"{_options.BaseUrl}/invitations/accept?token={notification.Token}",
                ExpiryHours = _options.InvitationTokenExpiryHours,
            },
            ct
        );

        await _emailQueue.EnqueueAsync(
            new EmailMessage(
                notification.Email,
                $"You've been invited to {notification.TenantName}",
                html
            ),
            ct
        );
    }

    public async Task Handle(UserRoleChangedNotification notification, CancellationToken ct)
    {
        var html = await _templateRenderer.RenderAsync(
            EmailTemplateNames.UserRoleChanged,
            new
            {
                notification.Username,
                notification.OldRole,
                notification.NewRole,
            },
            ct
        );

        await _emailQueue.EnqueueAsync(
            new EmailMessage(notification.Email, "Your role has been updated", html),
            ct
        );
    }
}
