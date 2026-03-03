using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;

namespace APITemplate.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddProblemDetails(ApiProblemDetailsOptions.Configure);

        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
        });

        return services;
    }
}
