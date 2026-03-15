using APITemplate.Extensions;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args); // Build host, configuration, and DI container.
    builder.AddApplicationRedaction();

    builder.Host.UseSerilog(
        (context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .AddOpenTelemetrySinks(context.Configuration, context.HostingEnvironment);
        }
    );

    builder.Services.AddApiFoundation(builder.Configuration); // Registers exception handling services (AddExceptionHandler + ProblemDetails), activated later in UseApiPipeline.
    builder.Services.AddObservability(builder.Configuration, builder.Environment); // Register OpenTelemetry tracing/metrics and environment-specific exporters.
    builder.Services.AddAuthenticationOptions(builder.Configuration, builder.Environment);
    builder.Services.AddPersistence(builder.Configuration); // Register EF Core + repositories + relational health checks.
    builder.Services.AddApplicationServices(); // Register application services + validators.
    builder.Services.AddEmailServices(builder.Configuration); // Register email sending infrastructure (SMTP, templates, queue, background service).
    builder.Services.AddMongoDB(builder.Configuration); // Register Mongo context/services + Mongo health checks.
    builder.Services.AddKeycloakBffAuthentication(builder.Configuration, builder.Environment); // Register Keycloak hybrid JWT + BFF authentication.
    builder.Services.AddApiVersioningConfiguration(); // Register API versioning and explorer metadata.
    builder.Services.AddGraphQLConfiguration(); // Register GraphQL schema and server services.

    var app = builder.Build(); // Materialize the web app from configured services.
    app.Logger.LogInformation("Starting APITemplate"); // Startup banner for diagnostics after logging pipeline is ready.

    await app.UseDatabaseAsync(); // Apply SQL/Mongo migrations before serving traffic.
    await app.WaitForKeycloakAsync(); // Wait for Keycloak to be reachable before serving traffic.

    app.UseApiPipeline(); // Configure middleware order for request processing.
    app.MapApplicationEndpoints(); // Map REST/GraphQL/health endpoints.

    app.Run(); // Start HTTP server and block until shutdown.
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Console.Error.WriteLine($"Application terminated unexpectedly: {ex}");
    throw;
}

public partial class Program; // Used by integration tests via WebApplicationFactory.
