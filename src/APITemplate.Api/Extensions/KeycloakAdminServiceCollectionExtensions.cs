using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Security;
using Keycloak.AuthServices.Sdk;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace APITemplate.Extensions;

public static class KeycloakAdminServiceCollectionExtensions
{
    public static IServiceCollection AddKeycloakAdminService(
        this IServiceCollection services)
    {
        // Populate KeycloakAdminClientOptions from IOptions<KeycloakOptions> at runtime,
        // so validation runs through the IOptions pipeline rather than raw IConfiguration.
        services.AddOptions<KeycloakAdminClientOptions>()
            .Configure<IOptions<KeycloakOptions>>((adminOpts, keycloakOpts) =>
            {
                adminOpts.AuthServerUrl = keycloakOpts.Value.AuthServerUrl;
                adminOpts.Realm = keycloakOpts.Value.Realm;
            });

        services.AddSingleton<KeycloakAdminTokenProvider>();
        services.AddTransient<KeycloakAdminTokenHandler>();

        // Pass a no-op action so the SDK registers its IKeycloakClient registrations and
        // the named HttpClient; the actual option values come from the Configure call above.
        services.AddKeycloakAdminHttpClient(_ => { })
            .AddHttpMessageHandler<KeycloakAdminTokenHandler>()
            .AddResilienceHandler(
                ResiliencePipelineKeys.KeycloakAdmin,
                builder =>
                {
                    builder.AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromSeconds(1),
                        UseJitter = true,
                    });
                });

        services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();

        return services;
    }
}
