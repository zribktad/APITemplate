namespace APITemplate.Application.Common.Security;

public interface IKeycloakAdminService
{
    Task<string> CreateUserAsync(string username, string email, CancellationToken ct = default);
    Task SendPasswordResetEmailAsync(string keycloakUserId, CancellationToken ct = default);
    Task SetUserEnabledAsync(string keycloakUserId, bool enabled, CancellationToken ct = default);
    Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default);
}
