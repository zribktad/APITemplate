using System.Diagnostics;
using System.Reflection;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Observability;
using HotChocolate.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace APITemplate.Extensions;

public static class ObservabilityServiceCollectionExtensions
{
    public const string ObservabilitySectionName = "Observability";

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<ObservabilityOptions>(configuration.GetSection(ObservabilitySectionName));
        services.AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();
        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(15);
            options.Period = TimeSpan.FromSeconds(30);
        });

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var options = configuration.GetSection(ObservabilitySectionName).Get<ObservabilityOptions>() ?? new();
        var resourceAttributes = BuildResourceAttributes(options, environment);
        var enableConsoleExporter = IsConsoleExporterEnabled(options);
        var otlpEndpoints = GetEnabledOtlpEndpoints(options, environment);

        var openTelemetryBuilder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(resourceAttributes));

        openTelemetryBuilder.WithTracing(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments(TelemetryPathPrefixes.Health);
                })
                .AddHttpClientInstrumentation()
                .AddHotChocolateInstrumentation()
                .AddRedisInstrumentation()
                .AddNpgsql()
                .AddSource(TelemetryActivitySources.MongoDbDriverDiagnosticSources);

            ConfigureTracingExporters(builder, otlpEndpoints, enableConsoleExporter);
        });

        openTelemetryBuilder.WithMetrics(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter(
                    ObservabilityConventions.MeterName,
                    ObservabilityConventions.HealthMeterName,
                    TelemetryMeterNames.AspNetCoreHosting,
                    TelemetryMeterNames.AspNetCoreServerKestrel,
                    TelemetryMeterNames.AspNetCoreConnections,
                    TelemetryMeterNames.AspNetCoreRouting,
                    TelemetryMeterNames.AspNetCoreDiagnostics,
                    TelemetryMeterNames.AspNetCoreRateLimiting,
                    TelemetryMeterNames.AspNetCoreAuthentication,
                    TelemetryMeterNames.AspNetCoreAuthorization)
                .AddView(TelemetryInstrumentNames.HttpServerRequestDuration, new ExplicitBucketHistogramConfiguration
                {
                    Boundaries =
                    [
                        0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 10
                    ]
                })
                .AddView(TelemetryInstrumentNames.HttpClientRequestDuration, new ExplicitBucketHistogramConfiguration
                {
                    Boundaries =
                    [
                        0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 10
                    ]
                })
                .AddView(TelemetryMetricNames.OutputCacheInvalidationDuration, new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000]
                })
                .AddView(TelemetryMetricNames.GraphQlRequestDuration, new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
                });

            ConfigureMetricExporters(builder, otlpEndpoints, enableConsoleExporter);
        });

        return services;
    }

    internal static IReadOnlyList<string> GetEnabledOtlpEndpoints(
        ObservabilityOptions options,
        IHostEnvironment environment)
    {
        var endpoints = new List<string>();

        if (IsAspireExporterEnabled(options, environment))
        {
            var aspireEndpoint = string.IsNullOrWhiteSpace(options.Aspire.Endpoint)
                ? TelemetryDefaults.AspireOtlpEndpoint
                : options.Aspire.Endpoint;
            endpoints.Add(aspireEndpoint);
        }

        if (IsOtlpExporterEnabled(options, environment) && !string.IsNullOrWhiteSpace(options.Otlp.Endpoint))
        {
            endpoints.Add(options.Otlp.Endpoint);
        }

        return endpoints
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static bool IsAspireExporterEnabled(ObservabilityOptions options, IHostEnvironment environment)
        => options.Exporters.Aspire.Enabled ?? (environment.IsDevelopment() && !IsRunningInContainer());

    internal static bool IsOtlpExporterEnabled(ObservabilityOptions options, IHostEnvironment environment)
        => options.Exporters.Otlp.Enabled ?? IsRunningInContainer();

    internal static bool IsConsoleExporterEnabled(ObservabilityOptions options)
        => options.Exporters.Console.Enabled ?? false;

    private static bool IsRunningInContainer()
        => string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object> BuildResourceAttributes(
        ObservabilityOptions options,
        IHostEnvironment environment)
    {
        var serviceName = string.IsNullOrWhiteSpace(options.ServiceName)
            ? ObservabilityConventions.ActivitySourceName
            : options.ServiceName;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? TelemetryDefaults.Unknown;

        return new Dictionary<string, object>
        {
            [TelemetryResourceAttributeKeys.ServiceName] = serviceName,
            [TelemetryResourceAttributeKeys.ServiceVersion] = version,
            [TelemetryResourceAttributeKeys.ServiceInstanceId] = Environment.MachineName,
            [TelemetryResourceAttributeKeys.DeploymentEnvironmentName] = environment.EnvironmentName
        };
    }

    private static void ConfigureTracingExporters(
        TracerProviderBuilder builder,
        IReadOnlyList<string> otlpEndpoints,
        bool enableConsoleExporter)
    {
        foreach (var endpoint in otlpEndpoints)
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
        }

        if (enableConsoleExporter)
        {
            builder.AddConsoleExporter();
        }
    }

    private static void ConfigureMetricExporters(
        MeterProviderBuilder builder,
        IReadOnlyList<string> otlpEndpoints,
        bool enableConsoleExporter)
    {
        foreach (var endpoint in otlpEndpoints)
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
        }

        if (enableConsoleExporter)
        {
            builder.AddConsoleExporter();
        }
    }
}
