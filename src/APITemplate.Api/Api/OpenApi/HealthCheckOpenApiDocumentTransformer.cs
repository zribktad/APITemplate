using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

public sealed class HealthCheckOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Paths ??= new OpenApiPaths();

        var pathItem = new OpenApiPathItem();
        pathItem.AddOperation(HttpMethod.Get, new OpenApiOperation
        {
            Tags = new HashSet<OpenApiTagReference> { new OpenApiTagReference("Health") },
            Summary = "Health check",
            Description = "Returns the health status of all registered services.",
            Responses = new OpenApiResponses
            {
                [StatusCodes.Status200OK.ToString()] = new OpenApiResponse { Description = "Healthy - all services are running" },
                [StatusCodes.Status503ServiceUnavailable.ToString()] = new OpenApiResponse { Description = "Unhealthy - one or more services are down" }
            }
        });

        document.Paths["/health"] = pathItem;

        return Task.CompletedTask;
    }
}
