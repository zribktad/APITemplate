using APITemplate.Application.Common.Security;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace APITemplate.Tests.Integration.Helpers;

internal static class TestServiceHelper
{
    internal static void ConfigureTestAuthentication(IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = null;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "http://localhost:8180/realms/api-template",
                ValidateAudience = true,
                ValidAudience = "api-template",
                ValidateLifetime = true,
                IssuerSigningKey = IntegrationAuthHelper.SecurityKey,
                ValidateIssuerSigningKey = true
            };
        });

        services.PostConfigure<OpenIdConnectOptions>(BffAuthenticationSchemes.Oidc, options =>
        {
            options.MetadataAddress = null;
            options.Authority = null;
            options.RequireHttpsMetadata = false;
            options.Configuration = new OpenIdConnectConfiguration
            {
                Issuer = "http://localhost:8180/realms/api-template",
                AuthorizationEndpoint = "http://localhost:8180/realms/api-template/protocol/openid-connect/auth",
                TokenEndpoint = "http://localhost:8180/realms/api-template/protocol/openid-connect/token",
                EndSessionEndpoint = "http://localhost:8180/realms/api-template/protocol/openid-connect/logout",
                UserInfoEndpoint = "http://localhost:8180/realms/api-template/protocol/openid-connect/userinfo"
            };
        });
    }

    internal static void RemoveExternalHealthChecks(IServiceCollection services)
    {
        services.Configure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(options =>
        {
            var toRemove = options.Registrations
                .Where(r => r.Name is "mongodb" or "keycloak" or "postgresql")
                .ToList();
            foreach (var r in toRemove)
                options.Registrations.Remove(r);
        });
    }

    internal static void MockMongoServices(IServiceCollection services)
    {
        services.RemoveAll(typeof(MongoDbContext));
        services.RemoveAll(typeof(IProductDataRepository));
        services.AddSingleton(new Mock<IProductDataRepository>().Object);
    }
}
