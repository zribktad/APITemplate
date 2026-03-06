using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

public sealed class BearerSecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly KeycloakOptions _keycloak;

    public BearerSecuritySchemeDocumentTransformer(
        IAuthenticationSchemeProvider schemeProvider,
        IOptions<KeycloakOptions> keycloakOptions)
    {
        _schemeProvider = schemeProvider;
        _keycloak = keycloakOptions.Value;
    }

    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await _schemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer"))
            return;

        var authority = KeycloakUrlHelper.BuildAuthority(_keycloak.AuthServerUrl, _keycloak.Realm);

        var securityScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Keycloak OAuth2 Authorization Code flow",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"{authority}/protocol/openid-connect/auth"),
                    TokenUrl = new Uri($"{authority}/protocol/openid-connect/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"] = "OpenID Connect",
                        ["profile"] = "User profile",
                        ["email"] = "Email address"
                    }
                }
            }
        };

        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["OAuth2"] = securityScheme;

        var requirement = new OpenApiSecurityRequirement();
        requirement[new OpenApiSecuritySchemeReference("OAuth2")] = new List<string> { "openid" };

        document.Security ??= [];
        document.Security.Add(requirement);
    }
}
