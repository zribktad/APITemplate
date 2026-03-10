using APITemplate.Api.Cache;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Observability;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Security;
using APITemplate.Api.Middleware;
using HealthChecks.UI.Client;
using Kot.MongoDB.Migrations;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Net.Http;

namespace APITemplate.Extensions;

public static class ApplicationBuilderExtensions
{
    public static async Task UseDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope(); // Resolve scoped infra services needed only during startup migration.

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // Resolve EF Core context for relational migrations.
        if (dbContext.Database.IsRelational())
            await StartupTelemetry.RunRelationalMigrationAsync(() => dbContext.Database.MigrateAsync());

        var seeder = scope.ServiceProvider.GetRequiredService<AuthBootstrapSeeder>();
        await StartupTelemetry.RunAuthBootstrapSeedAsync(() => seeder.SeedAsync());

        var mongoContext = scope.ServiceProvider.GetService<MongoDbContext>(); // Mongo context can be missing in tests.
        if (mongoContext is not null)
        {
            var migrator = scope.ServiceProvider.GetRequiredService<IMigrator>(); // Resolve Mongo migrator from DI.
            await StartupTelemetry.RunMongoMigrationAsync(() => migrator.MigrateAsync());
        }
    }

    /// <summary>
    /// Cross-cutting request context: correlation ID stamping, elapsed-time header, and
    /// structured Serilog request logging. Runs first so every downstream log entry is enriched.
    /// </summary>
    public static WebApplication UseRequestContextPipeline(this WebApplication app)
    {
        app.UseMiddleware<RequestContextMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.GetLevel = (httpContext, _, exception) =>
            {
                if (IsClientAbortedRequest(httpContext, exception))
                    return LogEventLevel.Information;

                if (exception is not null || httpContext.Response.StatusCode >= 500)
                    return LogEventLevel.Error;

                if (httpContext.Response.StatusCode >= 400)
                    return LogEventLevel.Warning;

                return LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            };
        });

        return app;
    }

    private static bool IsClientAbortedRequest(HttpContext httpContext, Exception? exception)
        => exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested;

    /// <summary>
    /// Identity and access-control pipeline: CORS preflight handling, token/cookie
    /// authentication, CSRF enforcement for BFF cookie sessions, and authorization policy
    /// evaluation. Order is fixed — each step depends on the one before it.
    /// </summary>
    public static WebApplication UseSecurityPipeline(this WebApplication app)
    {
        app.UseCors();            // CORS preflight must precede authentication.
        app.UseAuthentication();  // Populate HttpContext.User from JWT / cookie.
        app.UseMiddleware<CsrfValidationMiddleware>(); // Require X-CSRF header for cookie-authenticated mutations.
        app.UseAuthorization();   // Enforce endpoint authorization policies.

        return app;
    }

    public static async Task WaitForKeycloakAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var keycloak = app.Services.GetRequiredService<IOptions<KeycloakOptions>>().Value;

        if (string.IsNullOrEmpty(keycloak.AuthServerUrl) || string.IsNullOrEmpty(keycloak.Realm))
        {
            app.Logger.KeycloakConfigMissing();
            return;
        }

        if (app.Configuration.GetValue<bool>("Keycloak:SkipReadinessCheck"))
        {
            app.Logger.KeycloakReadinessCheckSkipped();
            return;
        }

        var discoveryUrl = KeycloakUrlHelper.BuildDiscoveryUrl(keycloak.AuthServerUrl, keycloak.Realm);
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        const int maxRetries = 30;
        const int delayMs = 2000;
        Exception? lastException = null;

        await StartupTelemetry.WaitForKeycloakReadinessAsync(
            maxRetries,
            async attempt =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    lastException = null;
                    var response = await httpClient.GetAsync(discoveryUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        app.Logger.KeycloakReady(keycloak.AuthServerUrl);
                        return true;
                    }

                    lastException = new HttpRequestException(
                        $"Keycloak readiness probe returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    lastException = ex;
                }

                if (attempt == maxRetries)
                {
                    if (lastException is not null)
                        app.Logger.KeycloakUnavailable(lastException, maxRetries);

                    return false;
                }

                app.Logger.KeycloakRetrying(attempt, maxRetries);
                await Task.Delay(delayMs, cancellationToken);
                return false;
            },
            () => new InvalidOperationException(
                $"Keycloak at {keycloak.AuthServerUrl} did not become available after {maxRetries} retries."));
    }

    /// <summary>
    /// Builds the HTTP middleware pipeline in execution order.
    /// </summary>
    /// <remarks>
    /// Exception handling is intentionally first so downstream middleware and endpoints
    /// are wrapped by the global handler. <c>app.UseExceptionHandler()</c> activates
    /// handlers previously registered in DI (for example via <c>AddExceptionHandler&lt;T&gt;()</c>).
    /// </remarks>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();         // Global exception handling — must be outermost.
        app.UseRequestContextPipeline();   // Correlation enrichment + structured request logging.
        app.UseApiDocumentation();         // Scalar / OpenAPI — development only.
        app.UseHttpsRedirection();
        app.UseSecurityPipeline();         // CORS → Authentication → CSRF → Authorization.
        app.UseRateLimiter();
        app.UseOutputCache();

        return app;
    }

    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        app.MapControllers().RequireRateLimiting(CachePolicyNames.RateLimitPolicy);
        app.MapGraphQL();
        app.MapNitroApp("/graphql/ui");
        app.UseHealthChecks();

        return app;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app; // Keep interactive API docs available only in development.

        var keycloak = app.Services.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        var authority = KeycloakUrlHelper.BuildAuthority(keycloak.AuthServerUrl, keycloak.Realm);

        app.MapOpenApi().AllowAnonymous(); // Map OpenAPI JSON endpoint.
        app.MapScalarApiReference("/scalar", (options, httpContext) =>
        {
            var redirectUri = BuildScalarRedirectUri(httpContext.Request);

            options.WithTitle("APITemplate");
            options
                .AddPreferredSecuritySchemes(AuthConstants.OpenApi.OAuth2Scheme)
                .AddAuthorizationCodeFlow(AuthConstants.OpenApi.OAuth2Scheme, flow =>
                {
                    flow.ClientId = AuthConstants.OpenApi.ScalarClientId;
                    flow.SelectedScopes = [.. AuthConstants.Scopes.Default];
                    flow.AuthorizationUrl = $"{authority}/{AuthConstants.OpenIdConnect.AuthorizationEndpointPath}";
                    flow.TokenUrl = $"{authority}/{AuthConstants.OpenIdConnect.TokenEndpointPath}";
                    flow.RedirectUri = redirectUri;
                    flow.Pkce = Pkce.Sha256;
                });
        }).AllowAnonymous();

        return app;
    }

    private static string BuildScalarRedirectUri(HttpRequest request)
        => $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";

    public static WebApplication UseHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        })
        .WithTags("Health")
        .WithSummary("Health check")
        .WithDescription("Returns the health status of all registered services.")
        .AllowAnonymous();

        return app;
    }
}
