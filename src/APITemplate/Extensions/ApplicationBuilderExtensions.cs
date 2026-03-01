using APITemplate.Api.Middleware;
using Scalar.AspNetCore;
using Serilog;

namespace APITemplate.Extensions;

public static class ApplicationBuilderExtensions
{
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
        app.MapScalarApiReference("/", options =>
        {
            options.WithTitle("APITemplate")
                   .AddHttpAuthentication("Bearer", scheme =>
                   {
                       scheme.Token = string.Empty;
                   });
        });

        return app;
    }
}
