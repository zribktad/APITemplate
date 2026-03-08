using APITemplate.Application.Common.Security;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

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
                .Where(r => r.Name is "mongodb" or "keycloak" or "postgresql" or "valkey")
                .ToList();
            foreach (var r in toRemove)
                options.Registrations.Remove(r);
        });
    }

    internal static void ReplaceOutputCacheWithInMemory(IServiceCollection services)
    {
        // Remove Valkey-backed cache services so tests use in-memory implementations
        // and observability startup does not try to connect to a real Redis instance.
        services.RemoveAll<IOutputCacheStore>();
        services.RemoveAll<IConnectionMultiplexer>();
        services.AddOutputCache();
        services.RemoveAll<IValidateOptions<Application.Common.Options.ValkeyOptions>>();
        services.RemoveAll<IOptionsChangeTokenSource<Application.Common.Options.ValkeyOptions>>();
    }

    internal static void ReplaceDataProtectionWithInMemory(IServiceCollection services)
    {
        // Replace Valkey-backed DataProtection with EphemeralDataProtectionProvider (no key persistence).
        services.RemoveAll<IDataProtectionProvider>();
        services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();
    }

    internal static void ReplaceTicketStoreWithInMemory(IServiceCollection services)
    {
        // Replace Redis-backed IDistributedCache with in-memory so ValkeyTicketStore
        // works without a real Valkey instance in tests.
        services.RemoveAll<IDistributedCache>();
        services.AddDistributedMemoryCache();
        services.RemoveAll<ValkeyTicketStore>();
        services.AddSingleton<ValkeyTicketStore>();
    }

    internal static void MockMongoServices(IServiceCollection services)
    {
        services.RemoveAll(typeof(MongoDbContext));
        services.RemoveAll(typeof(IProductDataRepository));
        var mock = new Mock<IProductDataRepository>();
        services.AddSingleton(mock);
        services.AddSingleton(mock.Object);
    }

    internal static void ReplaceProductRepositoryWithInMemory(IServiceCollection services)
    {
        services.RemoveAll(typeof(IProductRepository));
        services.AddScoped<IProductRepository, InMemoryProductRepository>();
    }
}
