using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Security;
using Keycloak.AuthServices.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace APITemplate.Extensions;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection("Cors"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .ToArray();

        if (corsOrigins?.Length > 0)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(corsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        services.AddOptions<BffOptions>()
            .Bind(configuration.GetSection("Bff"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SystemIdentityOptions>()
            .Bind(configuration.GetSection("SystemIdentity"))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.DefaultActorId),
                "SystemIdentity:DefaultActorId is required")
            .ValidateOnStart();

        services.AddOptions<BootstrapTenantOptions>()
            .Bind(configuration.GetSection("Bootstrap:Tenant"))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Code) && !string.IsNullOrWhiteSpace(o.Name),
                "Bootstrap tenant code/name is required")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddKeycloakBffAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var keycloakSection = configuration.GetSection("Keycloak");
        var authServerUrl = keycloakSection["auth-server-url"]!;
        var realm = keycloakSection["realm"]!;
        var authority = KeycloakUrlHelper.BuildAuthority(authServerUrl, realm);
        var bffOptions = configuration.GetSection("Bff").Get<BffOptions>() ?? new BffOptions();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = keycloakSection["resource"];
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = TenantClaimValidator.OnTokenValidated
                };
            })
            .AddCookie(BffAuthenticationSchemes.Cookie, options =>
            {
                options.Cookie.Name = bffOptions.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(bffOptions.SessionTimeoutMinutes);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(BffAuthenticationSchemes.Oidc, options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = false;
                options.ClientId = keycloakSection["resource"];
                options.ClientSecret = keycloakSection["credentials:secret"];
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SignInScheme = BffAuthenticationSchemes.Cookie;

                options.Scope.Clear();
                foreach (var scope in bffOptions.Scopes)
                    options.Scope.Add(scope);

                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = TenantClaimValidator.OnTokenValidated
                };
            });

        services.AddKeycloakAuthorization(configuration)
            .AddAuthorizationBuilder()
            .AddPolicy(
                AuthorizationPolicies.PlatformAdminOnly,
                policy => policy.RequireRole(UserRole.PlatformAdmin.ToString()));

        services.AddHttpClient(nameof(KeycloakHealthCheck));
        services.AddHealthChecks()
            .AddCheck<KeycloakHealthCheck>("keycloak", tags: ["identity"]);

        return services;
    }
}
