using APITemplate.Api.Cache;
using APITemplate.Application.Common.Options;
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

namespace APITemplate.Extensions;

public static class ApplicationBuilderExtensions
{
    public static async Task UseDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope(); // Resolve scoped infra services needed only during startup migration.

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // Resolve EF Core context for relational migrations.
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(); // Run pending relational migrations.
        }

        var seeder = scope.ServiceProvider.GetRequiredService<AuthBootstrapSeeder>();
        await seeder.SeedAsync();

        var mongoContext = scope.ServiceProvider.GetService<MongoDbContext>(); // Mongo context can be missing in tests.
        if (mongoContext is not null)
        {
            var migrator = scope.ServiceProvider.GetRequiredService<IMigrator>(); // Resolve Mongo migrator from DI.
            await migrator.MigrateAsync(); // Run pending Mongo migrations.
        }
    }

    public static WebApplication UseCustomMiddleware(this WebApplication app)
    {
        app.UseMiddleware<RequestContextMiddleware>(); // Enrich request/response context (correlation headers, elapsed time, log context).
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.GetLevel = (httpContext, _, exception) =>
            {
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
        }); // Add structured request logging for each HTTP request/response.

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
        var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient();

        const int maxRetries = 30;
        const int delayMs = 2000;

        for (var i = 1; i <= maxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await httpClient.GetAsync(discoveryUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    app.Logger.KeycloakReady(keycloak.AuthServerUrl);
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Keycloak not reachable yet
            }

            app.Logger.KeycloakRetrying(i, maxRetries);
            await Task.Delay(delayMs, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Keycloak at {keycloak.AuthServerUrl} did not become available after {maxRetries} retries.");
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
        app.UseExceptionHandler(); // Runtime activation of global exception middleware/handlers (registered during service setup).
        app.UseCustomMiddleware(); // Cross-cutting request context + structured request logging.
        app.UseApiDocumentation(); // Expose OpenAPI/Scalar only in development.
        app.UseHttpsRedirection(); // Redirect HTTP requests to HTTPS.
        app.UseCors(); // Apply global CORS policy before authentication middleware.
        app.UseAuthentication(); // Build HttpContext.User from token/identity handlers.
        app.UseAuthorization(); // Enforce endpoint authorization policies against the authenticated principal.
        app.UseRateLimiter(); // Apply rate limiting after authorization.
        app.UseOutputCache(); // Serve cached GET responses after auth/rate limiting.

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

        app.MapOpenApi().AllowAnonymous(); // Map OpenAPI JSON endpoint.
        app.MapScalarApiReference("/scalar", options =>
        {
            options.WithTitle("APITemplate");
            options
                .AddPreferredSecuritySchemes("OAuth2")
                .AddAuthorizationCodeFlow("OAuth2", flow =>
                {
                    flow.ClientId = "api-template-scalar";
                    flow.SelectedScopes = ["openid", "profile", "email"];
                });
        }).AllowAnonymous();

        return app;
    }

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
