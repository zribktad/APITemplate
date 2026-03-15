using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Keycloak.AuthServices.Sdk.Admin.Requests.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Security;

public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private readonly IKeycloakUserClient _userClient;
    private readonly string _realm;
    private readonly ILogger<KeycloakAdminService> _logger;

    public KeycloakAdminService(
        IKeycloakUserClient userClient,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakAdminService> logger)
    {
        _userClient = userClient;
        _realm = keycloakOptions.Value.Realm;
        _logger = logger;
    }

    public async Task<string> CreateUserAsync(string username, string email, CancellationToken ct = default)
    {
        var user = new UserRepresentation
        {
            Username = username,
            Email = email,
            Enabled = true,
            EmailVerified = false,
        };

        using var response = await _userClient.CreateUserWithResponseAsync(_realm, user, ct);
        response.EnsureSuccessStatusCode();

        var keycloakUserId = ExtractUserIdFromLocation(response);

        _logger.LogInformation(
            "Created Keycloak user {Username} with id {KeycloakUserId}",
            username,
            keycloakUserId);

        // NOTE: If ExecuteActionsEmailAsync fails, the user will already exist in Keycloak
        // but will have no setup email. Callers should handle HttpRequestException and
        // retry via SendPasswordResetEmailAsync(keycloakUserId) as a recovery path.
        await _userClient.ExecuteActionsEmailAsync(
            _realm,
            keycloakUserId,
            new ExecuteActionsEmailRequest
            {
                Actions = ["VERIFY_EMAIL", "UPDATE_PASSWORD"],
            },
            ct);

        return keycloakUserId;
    }

    public async Task SendPasswordResetEmailAsync(string keycloakUserId, CancellationToken ct = default)
    {
        await _userClient.ExecuteActionsEmailAsync(
            _realm,
            keycloakUserId,
            new ExecuteActionsEmailRequest
            {
                Actions = ["UPDATE_PASSWORD"],
            },
            ct);

        _logger.LogInformation(
            "Sent password reset email to Keycloak user {KeycloakUserId}",
            keycloakUserId);
    }

    public async Task SetUserEnabledAsync(string keycloakUserId, bool enabled, CancellationToken ct = default)
    {
        var patch = new UserRepresentation { Enabled = enabled };
        await _userClient.UpdateUserAsync(_realm, keycloakUserId, patch, ct);

        _logger.LogInformation(
            "Set Keycloak user {KeycloakUserId} enabled={Enabled}",
            keycloakUserId,
            enabled);
    }

    public async Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        try
        {
            await _userClient.DeleteUserAsync(_realm, keycloakUserId, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Treat 404 as success — the user was already deleted (e.g., retry scenario)
            _logger.LogWarning(
                "Keycloak user {KeycloakUserId} was not found during delete — treating as already deleted.",
                keycloakUserId);
            return;
        }

        _logger.LogInformation(
            "Deleted Keycloak user {KeycloakUserId}",
            keycloakUserId);
    }

    private static string ExtractUserIdFromLocation(HttpResponseMessage response)
    {
        var location = response.Headers.Location
            ?? throw new InvalidOperationException(
                "Keycloak CreateUser response did not include a Location header.");

        // Location is: {base}/admin/realms/{realm}/users/{id}
        var userId = location.Segments[^1].TrimEnd('/');

        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException(
                $"Could not extract user ID from Keycloak Location header: {location}");

        return userId;
    }
}
