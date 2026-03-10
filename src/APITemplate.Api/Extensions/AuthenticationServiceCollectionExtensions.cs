using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Observability;
using APITemplate.Infrastructure.Security;
using Keycloak.AuthServices.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
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
            .ValidateOnStart();

        services.AddOptions<BootstrapTenantOptions>()
            .Bind(configuration.GetSection("Bootstrap:Tenant"))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Code) && !string.IsNullOrWhiteSpace(o.Name),
                "Bootstrap tenant code/name is required")
            .ValidateOnStart();

        services.AddOptions<KeycloakOptions>()
            .Bind(configuration.GetSection("Keycloak"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddKeycloakBffAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var authSettings = BuildAuthSettings(configuration);

        ConfigureAuthenticationSchemes(services, authSettings, environment);
        ConfigureCookieSessionStore(services);
        ConfigureAuthorization(services, configuration);
        ConfigureKeycloakInfrastructure(services);

        return services;
    }

    private static AuthSettings BuildAuthSettings(IConfiguration configuration)
    {
        var keycloak = configuration.GetSection("Keycloak").Get<KeycloakOptions>()
            ?? throw new InvalidOperationException("Keycloak configuration section is missing.");
        var bffOptions = configuration.GetSection("Bff").Get<BffOptions>() ?? new BffOptions();
        var authority = KeycloakUrlHelper.BuildAuthority(keycloak.AuthServerUrl, keycloak.Realm);
        return new AuthSettings(keycloak, bffOptions, authority);
    }

    private static void ConfigureAuthenticationSchemes(
        IServiceCollection services,
        AuthSettings settings,
        IHostEnvironment environment)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => ConfigureJwtBearer(options, settings, environment))
            .AddCookie(BffAuthenticationSchemes.Cookie, options => ConfigureCookie(options, settings, environment))
            .AddOpenIdConnect(BffAuthenticationSchemes.Oidc, options => ConfigureOpenIdConnect(options, settings, environment));
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        AuthSettings settings,
        IHostEnvironment environment)
    {
        var isDevelopment = environment.IsDevelopment();

        options.Authority = settings.Authority;
        options.Audience = settings.Keycloak.Resource;
        options.RequireHttpsMetadata = !isDevelopment;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            LogTokenId = isDevelopment,
            LogValidationExceptions = isDevelopment,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            RequireAudience = true,
            SaveSigninToken = false,
            TryAllDecryptionKeys = true,
            TryAllIssuerSigningKeys = true,
            ValidateActor = false,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidateTokenReplay = false,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = TenantClaimValidator.OnTokenValidated
        };
    }

    private static void ConfigureCookie(
        CookieAuthenticationOptions options,
        AuthSettings settings,
        IHostEnvironment environment)
    {
        options.Cookie.Name = settings.Bff.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(settings.Bff.SessionTimeoutMinutes);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = RejectUnauthorizedRedirectAsync;
        options.Events.OnValidatePrincipal = CookieSessionRefresher.OnValidatePrincipal;
    }

    private static void ConfigureOpenIdConnect(
        OpenIdConnectOptions options,
        AuthSettings settings,
        IHostEnvironment environment)
    {
        options.Authority = settings.Authority;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.ClientId = settings.Keycloak.Resource;
        options.ClientSecret = settings.Keycloak.Credentials.Secret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.SignInScheme = BffAuthenticationSchemes.Cookie;

        options.Scope.Clear();
        foreach (var scope in settings.Bff.Scopes)
            options.Scope.Add(scope);

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = TenantClaimValidator.OnTokenValidated
        };
    }

    private static void ConfigureCookieSessionStore(IServiceCollection services)
    {
        services.AddSingleton<ValkeyTicketStore>();
        services.AddOptions<CookieAuthenticationOptions>(BffAuthenticationSchemes.Cookie)
            .Configure<ValkeyTicketStore>((opts, store) => opts.SessionStore = store);
    }

    private static void ConfigureAuthorization(IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeycloakAuthorization(configuration)
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, BffAuthenticationSchemes.Cookie)
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(
                AuthorizationPolicies.PlatformAdmin,
                policy => policy.RequireRole(UserRole.PlatformAdmin.ToString()));
    }

    private static void ConfigureKeycloakInfrastructure(IServiceCollection services)
    {
        services.AddHttpClient(nameof(KeycloakHealthCheck));
        services.AddHttpClient(AuthConstants.HttpClients.KeycloakToken);
        services.AddHealthChecks()
            .AddCheck<KeycloakHealthCheck>("keycloak", tags: ["identity"]);
    }

    private static Task RejectUnauthorizedRedirectAsync(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context)
    {
        AuthTelemetry.RecordUnauthorizedRedirect();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    private sealed record AuthSettings(
        KeycloakOptions Keycloak,
        BffOptions Bff,
        string Authority);
}
