using System.Security.Claims;
using System.Text.Encodings.Web;
using APITemplate.Application.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Tests.Integration;

public sealed class BffWebApplicationFactory : CustomWebApplicationFactory
{
    private TestBffClaims? _claimsOverride;

    public BffWebApplicationFactory WithClaims(TestBffClaims claims)
    {
        _claimsOverride = claims;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            var claims = _claimsOverride ?? new TestBffClaims(
                Guid.NewGuid(), Guid.NewGuid(),
                ["PlatformAdmin"], "test-user", "test@example.com");

            services.AddSingleton(claims);
            services.AddTransient<TestBffCookieAuthHandler>();
            services.AddTransient<TestBffOidcSignOutHandler>();

            // Replace the BffCookie and BffOidc scheme handlers with test handlers.
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                if (options.SchemeMap.TryGetValue(BffAuthenticationSchemes.Cookie, out var cookieScheme))
                    cookieScheme.HandlerType = typeof(TestBffCookieAuthHandler);

                if (options.SchemeMap.TryGetValue(BffAuthenticationSchemes.Oidc, out var oidcScheme))
                    oidcScheme.HandlerType = typeof(TestBffOidcSignOutHandler);
            });
        });
    }
}

public sealed record TestBffClaims(
    Guid UserId,
    Guid TenantId,
    string[] Roles,
    string Username,
    string Email,
    string? Name = null,
    Claim[]? ExtraClaims = null);

internal sealed class TestBffCookieAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TestBffClaims claims)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationSignOutHandler
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claimsList = new List<Claim>
        {
            new("sub", claims.UserId.ToString()),
            new(CustomClaimTypes.TenantId, claims.TenantId.ToString()),
            new("preferred_username", claims.Username),
            new("email", claims.Email),
            new("name", claims.Name ?? claims.Username)
        };

        foreach (var role in claims.Roles)
            claimsList.Add(new Claim("groups", role));

        if (claims.ExtraClaims is not null)
            claimsList.AddRange(claims.ExtraClaims);

        var identity = new ClaimsIdentity(claimsList, BffAuthenticationSchemes.Cookie);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, BffAuthenticationSchemes.Cookie);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    public Task SignOutAsync(AuthenticationProperties? properties)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// No-op OIDC handler for tests — handles sign-out by redirecting to the configured redirect URI.
/// </summary>
internal sealed class TestBffOidcSignOutHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationSignOutHandler
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());

    public Task SignOutAsync(AuthenticationProperties? properties)
    {
        if (properties?.RedirectUri is { } redirectUri)
        {
            Context.Response.Redirect(redirectUri);
        }
        return Task.CompletedTask;
    }
}
