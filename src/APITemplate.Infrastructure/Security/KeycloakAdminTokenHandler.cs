using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Security;

/// <summary>
/// A <see cref="DelegatingHandler"/> that acquires a Keycloak service-account (client credentials)
/// token and attaches it as a Bearer header to every outbound admin API request.
/// Tokens are cached in memory until they expire; a 30-second safety margin prevents
/// using a token that is about to expire mid-flight.
/// </summary>
public sealed class KeycloakAdminTokenHandler : DelegatingHandler
{
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<KeycloakOptions> _keycloakOptions;
    private readonly ILogger<KeycloakAdminTokenHandler> _logger;

    // Simple in-memory cache — handler is registered as Singleton.
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public KeycloakAdminTokenHandler(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakAdminTokenHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _keycloakOptions = keycloakOptions;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt - ExpiryMargin)
            return _cachedToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock.
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt - ExpiryMargin)
                return _cachedToken;

            var response = await FetchTokenAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(response.AccessToken))
                throw new InvalidOperationException("Keycloak token endpoint returned a response with an empty access_token.");

            _cachedToken = response.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<TokenResponse> FetchTokenAsync(CancellationToken cancellationToken)
    {
        var keycloak = _keycloakOptions.Value;
        var tokenEndpoint = BuildTokenEndpoint(keycloak);

        using var client = _httpClientFactory.CreateClient(AuthConstants.HttpClients.KeycloakToken);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [AuthConstants.OAuth2FormParameters.GrantType] = "client_credentials",
            [AuthConstants.OAuth2FormParameters.ClientId] = keycloak.Resource,
            [AuthConstants.OAuth2FormParameters.ClientSecret] = keycloak.Credentials.Secret,
        });

        using var response = await client.PostAsync(tokenEndpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to acquire Keycloak admin token. Status: {Status}. Body: {Body}",
                (int)response.StatusCode,
                body);
            response.EnsureSuccessStatusCode(); // throws
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak token endpoint returned empty body.");

        return token;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _lock.Dispose();
        base.Dispose(disposing);
    }

    private static string BuildTokenEndpoint(KeycloakOptions keycloak)
    {
        var authority = KeycloakUrlHelper.BuildAuthority(keycloak.AuthServerUrl, keycloak.Realm);
        return $"{authority.TrimEnd('/')}/{AuthConstants.OpenIdConnect.TokenEndpointPath}";
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
