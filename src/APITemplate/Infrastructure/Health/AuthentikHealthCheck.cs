using APITemplate.Application.Common.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Health;

public sealed class AuthentikHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly AuthentikOptions _options;

    public AuthentikHealthCheck(HttpClient httpClient, IOptions<AuthentikOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CheckTimeout);

            var discoveryUrl = _options.Authority.TrimEnd('/') + "/.well-known/openid-configuration";
            using var response = await _httpClient.GetAsync(discoveryUrl, cts.Token);
            response.EnsureSuccessStatusCode();

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Authentik OIDC discovery endpoint is not reachable", ex);
        }
    }
}
