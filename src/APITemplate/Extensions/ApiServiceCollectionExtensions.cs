using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;

namespace APITemplate.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers core API services (controllers, OpenAPI, ProblemDetails) and
    /// exception handling dependencies.
    /// </summary>
    /// <remarks>
    /// This method only registers exception handling services in DI
    /// (including <see cref="ApiExceptionHandler"/>). Runtime exception interception
    /// is activated later by calling <c>app.UseExceptionHandler()</c> in the middleware pipeline.
    /// </remarks>
    public static IServiceCollection AddApiFoundation(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddProblemDetails(ApiProblemDetailsOptions.Configure);

        // Registers the handler in DI; middleware activation happens in UseApiPipeline via app.UseExceptionHandler().
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesOperationTransformer>();
        });

        return services;
    }
}
