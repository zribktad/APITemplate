using APITemplate.Extensions;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args); // Build host, configuration, and DI container.
    builder.AddApplicationRedaction();

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services);
    });

    builder.Services.AddApiFoundation(); // Register REST/OpenAPI/ProblemDetails/exception handling foundation.
    builder.Services.AddAuthenticationOptions(builder.Configuration);
    builder.Services.AddPersistence(builder.Configuration); // Register EF Core + repositories + relational health checks.
    builder.Services.AddApplicationServices(); // Register application services + validators.
    builder.Services.AddMongoDB(builder.Configuration); // Register Mongo context/services + Mongo health checks.
    builder.Services.AddJwtAuthentication(); // Register JWT authentication/authorization handlers.
    builder.Services.AddApiVersioningConfiguration(); // Register API versioning and explorer metadata.
    builder.Services.AddGraphQLConfiguration(); // Register GraphQL schema and server services.

    var app = builder.Build(); // Materialize the web app from configured services.
    app.Logger.LogInformation("Starting APITemplate"); // Startup banner for diagnostics after logging pipeline is ready.

    await app.UseDatabaseAsync(); // Apply SQL/Mongo migrations before serving traffic.

    app.UseApiPipeline(); // Configure middleware order for request processing.
    app.MapApplicationEndpoints(); // Map REST/GraphQL/health endpoints.

    app.Run(); // Start HTTP server and block until shutdown.
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Application terminated unexpectedly: {ex}"); // Avoid static Serilog race in parallel test hosts.
}

public partial class Program; // Used by integration tests via WebApplicationFactory.
