using APITemplate.Infrastructure.Persistence;
using APITemplate.Api.Middleware;
using Kot.MongoDB.Migrations;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Text.Json;

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

        return app;
    }

    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapGraphQL();
        app.MapNitroApp("/graphql/ui");
        app.MapReverseProxy();
        app.UseHealthChecks();

        return app;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app; // Keep interactive API docs available only in development.

        app.MapOpenApi(); // Map OpenAPI JSON endpoint.
        app.MapScalarApiReference("/scalar", options =>
        {
            options.WithTitle("APITemplate");
            options.Authentication = new ScalarAuthenticationOptions
            {
                PreferredSecuritySchemes = ["Bearer"]
            };
        });

        return app;
    }

    public static WebApplication UseHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var result = new
                {
                    status = report.Status.ToString(),
                    services = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        tags = e.Value.Tags
                    })
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
        })
        .WithTags("Health")
        .WithSummary("Health check")
        .WithDescription("Returns the health status of all registered services.")
        .AllowAnonymous();

        return app;
    }
}
