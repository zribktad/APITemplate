using MediatR;

namespace APITemplate.Application.Common.Events;

public sealed record UserRegisteredNotification(Guid UserId, string Email, string Username)
    : INotification;

public sealed record TenantInvitationCreatedNotification(
    Guid InvitationId,
    string Email,
    string TenantName,
    string Token
) : INotification;

public sealed record UserRoleChangedNotification(
    Guid UserId,
    string Email,
    string Username,
    string OldRole,
    string NewRole
) : INotification;
