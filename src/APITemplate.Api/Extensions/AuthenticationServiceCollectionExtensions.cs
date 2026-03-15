using APITemplate.Api.Authorization;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Options;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Observability;
using APITemplate.Infrastructure.Security;
using Keycloak.AuthServices.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Polly;

namespace APITemplate.Extensions;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        var corsSection = configuration.SectionFor<CorsOptions>();
        services
            .AddOptions<CorsOptions>()
            .Bind(corsSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var corsOrigins = (corsSection.Get<CorsOptions>() ?? new CorsOptions())
            .AllowedOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .ToArray();

        if (corsOrigins?.Length > 0)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .WithOrigins(corsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        services
            .AddOptions<BffOptions>()
            .Bind(configuration.SectionFor<BffOptions>())
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<SystemIdentityOptions>()
            .Bind(configuration.SectionFor<SystemIdentityOptions>())
            .ValidateOnStart();

        services
            .AddOptions<BootstrapTenantOptions>()
            .Bind(configuration.SectionFor<BootstrapTenantOptions>())
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Code) && !string.IsNullOrWhiteSpace(o.Name),
                "Bootstrap tenant code/name is required"
            )
            .ValidateOnStart();

        services
            .AddOptions<KeycloakOptions>()
            .Bind(configuration.SectionFor<KeycloakOptions>())
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddKeycloakBffAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        var authSettings = BuildAuthSettings(configuration);

        ConfigureAuthenticationSchemes(services, authSettings, environment);
        ConfigureCookieSessionStore(services);
        ConfigureAuthorization(services, configuration);
        ConfigureKeycloakInfrastructure(services, configuration);

        return services;
    }

    private static AuthSettings BuildAuthSettings(IConfiguration configuration)
    {
        var keycloak =
            configuration.SectionFor<KeycloakOptions>().Get<KeycloakOptions>()
            ?? throw new InvalidOperationException("Keycloak configuration section is missing.");
        var bffOptions =
            configuration.SectionFor<BffOptions>().Get<BffOptions>() ?? new BffOptions();
        var authority = KeycloakUrlHelper.BuildAuthority(keycloak.AuthServerUrl, keycloak.Realm);
        return new AuthSettings(keycloak, bffOptions, authority);
    }

    private static void ConfigureAuthenticationSchemes(
        IServiceCollection services,
        AuthSettings settings,
        IHostEnvironment environment
    )
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => ConfigureJwtBearer(options, settings, environment))
            .AddCookie(
                AuthConstants.BffSchemes.Cookie,
                options => ConfigureCookie(options, settings, environment)
            )
            .AddOpenIdConnect(
                AuthConstants.BffSchemes.Oidc,
                options => ConfigureOpenIdConnect(options, settings, environment)
            );
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        AuthSettings settings,
        IHostEnvironment environment
    )
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
            ClockSkew = TimeSpan.FromMinutes(5),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = TenantClaimValidator.OnTokenValidated,
        };
    }

    private static void ConfigureCookie(
        CookieAuthenticationOptions options,
        AuthSettings settings,
        IHostEnvironment environment
    )
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
        IHostEnvironment environment
    )
    {
        options.Authority = settings.Authority;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.ClientId = settings.Keycloak.Resource;
        options.ClientSecret = settings.Keycloak.Credentials.Secret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.SignInScheme = AuthConstants.BffSchemes.Cookie;

        options.Scope.Clear();
        foreach (var scope in settings.Bff.Scopes)
            options.Scope.Add(scope);

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = TenantClaimValidator.OnTokenValidated,
        };
    }

    private static void ConfigureCookieSessionStore(IServiceCollection services)
    {
        services.AddSingleton<ValkeyTicketStore>();
        services
            .AddOptions<CookieAuthenticationOptions>(AuthConstants.BffSchemes.Cookie)
            .Configure<ValkeyTicketStore>((opts, store) => opts.SessionStore = store);
    }

    private static void ConfigureAuthorization(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<IRolePermissionMap, StaticRolePermissionMap>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        var authBuilder = services
            .AddKeycloakAuthorization(configuration)
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        AuthConstants.BffSchemes.Cookie
                    )
                    .RequireAuthenticatedUser()
                    .Build()
            )
            .AddPolicy(
                AuthConstants.Policies.PlatformAdmin,
                policy =>
                    policy
                        .AddAuthenticationSchemes(
                            JwtBearerDefaults.AuthenticationScheme,
                            AuthConstants.BffSchemes.Cookie
                        )
                        .RequireAuthenticatedUser()
                        .RequireRole(UserRole.PlatformAdmin.ToString())
            )
            .AddPolicy(
                AuthConstants.Policies.TenantAdmin,
                policy =>
                    policy
                        .AddAuthenticationSchemes(
                            JwtBearerDefaults.AuthenticationScheme,
                            AuthConstants.BffSchemes.Cookie
                        )
                        .RequireAuthenticatedUser()
                        .RequireRole(
                            UserRole.TenantAdmin.ToString(),
                            UserRole.PlatformAdmin.ToString()
                        )
            );

        foreach (var permission in Permission.All)
        {
            authBuilder.AddPolicy(
                permission,
                policy =>
                    policy
                        .AddAuthenticationSchemes(
                            JwtBearerDefaults.AuthenticationScheme,
                            AuthConstants.BffSchemes.Cookie
                        )
                        .RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(permission))
            );
        }
    }

    private static void ConfigureKeycloakInfrastructure(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddHttpClient(nameof(KeycloakHealthCheck));
        services.AddHttpClient(AuthConstants.HttpClients.KeycloakToken);
        services.AddHealthChecks().AddCheck<KeycloakHealthCheck>("keycloak", tags: ["identity"]);

        var keycloakOptions = configuration.SectionFor<KeycloakOptions>().Get<KeycloakOptions>()!;

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.KeycloakReadiness,
            builder =>
            {
                builder.AddRetry(
                    new()
                    {
                        MaxRetryAttempts = keycloakOptions.ReadinessMaxRetries - 1,
                        BackoffType = DelayBackoffType.Constant,
                        Delay = TimeSpan.FromSeconds(2),
                        ShouldHandle = new PredicateBuilder()
                            .Handle<HttpRequestException>()
                            .Handle<TaskCanceledException>(),
                    }
                );
            }
        );
    }

    private static Task RejectUnauthorizedRedirectAsync(
        Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context
    )
    {
        AuthTelemetry.RecordUnauthorizedRedirect();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    private sealed record AuthSettings(KeycloakOptions Keycloak, BffOptions Bff, string Authority);
}
