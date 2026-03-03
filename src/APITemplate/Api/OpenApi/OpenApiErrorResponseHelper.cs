using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

internal static class OpenApiErrorResponseHelper
{
    internal static void AddErrorResponse(OpenApiOperation operation, int statusCode, string? description = null)
    {
        var statusCodeKey = statusCode.ToString();
        operation.Responses ??= new OpenApiResponses();
        if (operation.Responses.ContainsKey(statusCodeKey))
            return;

        var resolvedDescription = string.IsNullOrWhiteSpace(description)
            ? ReasonPhrases.GetReasonPhrase(statusCode)
            : description;

        operation.Responses[statusCodeKey] = new OpenApiResponse
        {
            Description = resolvedDescription,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchemaReference("ApiProblemDetails", null)
                }
            }
        };
    }
}
