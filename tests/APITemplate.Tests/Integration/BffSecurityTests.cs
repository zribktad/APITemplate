using System.Net;
using System.Security.Claims;
using APITemplate.Application.Common.Security;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace APITemplate.Tests.Integration;

public sealed class BffSecurityTests
{
    private readonly BffSecurityWebApplicationFactory _factory;

    public BffSecurityTests(BffSecurityWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostWithCookieAuth_WithoutCsrfHeader_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Cookie-Auth", "1");

        var response = await client.PostAsync("/api/v1/products",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostWithCookieAuth_WithCsrfHeader_PassesCsrfCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Cookie-Auth", "1");
        client.DefaultRequestHeaders.Add(CsrfConstants.HeaderName, CsrfConstants.HeaderValue);

        var response = await client.PostAsync("/api/v1/products",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            ct);

        // CSRF passes; authorization middleware rejects the fake cookie identity (no real session),
        // so we expect 401 rather than 403 (which would mean CSRF blocked the request).
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWithJwtBearer_WithoutCsrfHeader_PassesCsrfCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(client);

        var response = await client.PostAsync("/api/v1/products",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCsrfEndpoint_ReturnsHeaderConfig()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/bff/csrf", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("X-CSRF", body);
        Assert.Contains("headerName", body);
        Assert.Contains("headerValue", body);
    }
}

/// <summary>
/// Extends <see cref="CustomWebApplicationFactory"/> with a startup filter that fabricates a
/// cookie-authenticated <see cref="System.Security.Claims.ClaimsPrincipal"/> when the
/// <c>X-Test-Cookie-Auth: 1</c> request header is present. This lets CSRF tests simulate
/// cookie sessions without a real Keycloak login.
/// </summary>
public sealed class BffSecurityWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, FakeCookieAuthStartupFilter>();
        });
    }
}

internal sealed class FakeCookieAuthStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            // Insert before the rest of the pipeline so we can pre-populate HttpContext.User.
            // UseAuthentication won't overwrite an already-authenticated user when the
            // default JWT Bearer scheme finds no token in the request.
            app.Use(async (ctx, nextMiddleware) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-Cookie-Auth", out _))
                {
                    var identity = new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "testuser"), new Claim(AuthConstants.Claims.Subject, Guid.NewGuid().ToString())],
                        BffAuthenticationSchemes.Cookie);
                    ctx.User = new ClaimsPrincipal(identity);
                }
                await nextMiddleware(ctx);
            });

            next(app);
        };
}
