using APITemplate.Extensions;
using Microsoft.OpenApi;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting APITemplate");

    var builder = WebApplication.CreateBuilder(args); 

    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = " JWT token (without 'Bearer ' prefix)"
            };
            return Task.CompletedTask;
        });
    });
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddApiVersioningConfiguration();
    builder.Services.AddGraphQLConfiguration();

    var app = builder.Build();

    app.UseCustomMiddleware();
    app.UseApiDocumentation();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGraphQL();
    app.MapNitroApp("/graphql/ui");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
