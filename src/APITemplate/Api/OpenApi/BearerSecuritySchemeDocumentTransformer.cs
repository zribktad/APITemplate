using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

public sealed class BearerSecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;

    public BearerSecuritySchemeDocumentTransformer(IAuthenticationSchemeProvider schemeProvider)
    {
        _schemeProvider = schemeProvider;
    }

    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await _schemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer"))
            return;

        var securityScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Bearer token from Keycloak"
        };

        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["Bearer"] = securityScheme;

        var requirement = new OpenApiSecurityRequirement();
        requirement[new OpenApiSecuritySchemeReference("Bearer")] = new List<string>();

        document.Security ??= [];
        document.Security.Add(requirement);
    }
}
