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
            await dbContext.Database.MigrateAsync(); // Run pending relational migrations.

        var mongoContext = scope.ServiceProvider.GetService<MongoDbContext>(); // Mongo context can be missing in tests.
        if (mongoContext is not null)
        {
            var migrator = scope.ServiceProvider.GetRequiredService<IMigrator>(); // Resolve Mongo migrator from DI.
            await migrator.MigrateAsync(); // Run pending Mongo migrations.
        }
    }

    public static WebApplication UseCustomMiddleware(this WebApplication app)
    {
        app.UseMiddleware<RequestContextMiddleware>(); // Add correlation/trace headers and request timing context.
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

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler(); // Enable centralized REST exception handling with ProblemDetails output.
        app.UseCustomMiddleware(); // Add project-specific cross-cutting middleware stack.
        app.UseApiDocumentation(); // Expose OpenAPI/Scalar only in development.
        app.UseHttpsRedirection(); // Redirect HTTP requests to HTTPS.
        app.UseCors(); // Apply global CORS policy before authentication middleware.
        app.UseAuthentication(); // Authenticate principal from JWT/token.
        app.UseAuthorization(); // Enforce authorization policies/attributes.

        return app;
    }

    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        app.MapControllers(); // Map versioned REST controllers.
        app.MapGraphQL(); // Map GraphQL endpoint.
        app.MapNitroApp("/graphql/ui"); // Map GraphQL UI (Nitro).
        app.UseHealthChecks(); // Map health endpoint with custom JSON response.

        return app;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app; // Keep interactive API docs available only in development.

        app.MapOpenApi(); // Map OpenAPI JSON endpoint.
        app.MapScalarApiReference("/scalar", options =>
        {
            options.WithTitle("APITemplate")
                   .AddHttpAuthentication("Bearer", scheme =>
                   {
                       scheme.Token = string.Empty;
                   });
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
