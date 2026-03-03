using APITemplate.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger(); // Bootstrap logger available before DI/container is built.

try
{
    Log.Information("Starting APITemplate"); // Startup banner for early diagnostics.

    var builder = WebApplication.CreateBuilder(args); // Build host, configuration, and DI container.

    builder.Host.UseSerilog(); // Route ASP.NET logging through Serilog sinks/enrichers.

    builder.Services.AddApiFoundation(); // Register REST/OpenAPI/ProblemDetails/exception handling foundation.
    builder.Services.AddPersistence(builder.Configuration); // Register EF Core + repositories + relational health checks.
    builder.Services.AddApplicationServices(); // Register application services + validators.
    builder.Services.AddMongoDB(builder.Configuration); // Register Mongo context/services + Mongo health checks.
    builder.Services.AddJwtAuthentication(builder.Configuration); // Register JWT authentication/authorization handlers.
    builder.Services.AddApiVersioningConfiguration(); // Register API versioning and explorer metadata.
    builder.Services.AddGraphQLConfiguration(); // Register GraphQL schema and server services.

    var app = builder.Build(); // Materialize the web app from configured services.

    await app.UseDatabaseAsync(); // Apply SQL/Mongo migrations before serving traffic.

    app.UseApiPipeline(); // Configure middleware order for request processing.
    app.MapApplicationEndpoints(); // Map REST/GraphQL/health endpoints.

    app.Run(); // Start HTTP server and block until shutdown.
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly"); // Fatal startup/runtime exception before graceful shutdown.
}
finally
{
    Log.CloseAndFlush(); // Ensure buffered logs are flushed on shutdown/crash.
}

public partial class Program; // Used by integration tests via WebApplicationFactory.
