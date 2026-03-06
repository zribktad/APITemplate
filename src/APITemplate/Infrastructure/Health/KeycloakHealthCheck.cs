using APITemplate.Infrastructure.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace APITemplate.Infrastructure.Health;

public sealed class KeycloakHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly string _discoveryUrl;

    public KeycloakHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(KeycloakHealthCheck));
        var authServerUrl = configuration["Keycloak:auth-server-url"]
                            ?? throw new InvalidOperationException("Keycloak:auth-server-url is not configured.");
        var realm = configuration["Keycloak:realm"]
                    ?? throw new InvalidOperationException("Keycloak:realm is not configured.");
        _discoveryUrl = KeycloakUrlHelper.BuildDiscoveryUrl(authServerUrl, realm);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CheckTimeout);

            var response = await _httpClient.GetAsync(_discoveryUrl, cts.Token);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Keycloak returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Keycloak is not reachable", ex);
        }
    }
}
