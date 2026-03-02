using APITemplate.Api.Middleware;
using APITemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json;

namespace APITemplate.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies all pending EF Core migrations at startup.
    /// Tables, indexes, and stored procedures are all versioned in migrations —
    /// a single call brings the database fully up to date.
    /// </summary>
    public static async Task UseDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public static WebApplication UseCustomMiddleware(this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        app.UseSerilogRequestLogging();

        return app;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi();
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
