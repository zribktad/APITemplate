using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.Configure<ObservabilityOptions>(configuration.SectionFor<ObservabilityOptions>());
        services.Configure<AppOptions>(configuration.SectionFor<AppOptions>());
        services.AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();
        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(15);
            options.Period = TimeSpan.FromMinutes(5);
        });

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var options = GetObservabilityOptions(configuration);
        var appOptions = GetAppOptions(configuration);
        var resourceAttributes = BuildResourceAttributes(appOptions, environment);
        var enableConsoleExporter = IsConsoleExporterEnabled(options);
        var otlpEndpoints = GetEnabledOtlpEndpoints(options, environment);

        var openTelemetryBuilder = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(resourceAttributes));

        openTelemetryBuilder.WithTracing(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext =>
                        !httpContext.Request.Path.StartsWithSegments(TelemetryPathPrefixes.Health);
                    options.EnrichWithHttpRequest = (activity, httpRequest) =>
                    {
                        if (
                            TelemetryApiSurfaceResolver.Resolve(httpRequest.Path)
                            != TelemetrySurfaces.Rest
                        )
                            return;

                        var route = HttpRouteResolver.Resolve(httpRequest.HttpContext);
                        activity.DisplayName = $"{httpRequest.Method} {route}";
                        activity.SetTag(TelemetryTagKeys.HttpRoute, route);
                    };
                })
                .AddHttpClientInstrumentation()
                .AddHotChocolateInstrumentation()
                .AddRedisInstrumentation()
                .AddNpgsql()
                .AddSource(ObservabilityConventions.ActivitySourceName)
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
                    TelemetryMeterNames.AspNetCoreAuthorization
                )
                .AddView(
                    TelemetryInstrumentNames.HttpServerRequestDuration,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries =
                        [
                            0.005,
                            0.01,
                            0.025,
                            0.05,
                            0.075,
                            0.1,
                            0.25,
                            0.5,
                            0.75,
                            1,
                            2.5,
                            5,
                            10,
                        ],
                    }
                )
                .AddView(
                    TelemetryInstrumentNames.HttpClientRequestDuration,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries =
                        [
                            0.005,
                            0.01,
                            0.025,
                            0.05,
                            0.075,
                            0.1,
                            0.25,
                            0.5,
                            0.75,
                            1,
                            2.5,
                            5,
                            10,
                        ],
                    }
                )
                .AddView(
                    TelemetryMetricNames.OutputCacheInvalidationDuration,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000],
                    }
                )
                .AddView(
                    TelemetryMetricNames.GraphQlRequestDuration,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000],
                    }
                );

            ConfigureMetricExporters(builder, otlpEndpoints, enableConsoleExporter);
        });

        return services;
    }

    internal static IReadOnlyList<string> GetEnabledOtlpEndpoints(
        ObservabilityOptions options,
        IHostEnvironment environment
    )
    {
        var endpoints = new List<string>();

        if (IsAspireExporterEnabled(options, environment))
        {
            var aspireEndpoint = string.IsNullOrWhiteSpace(options.Aspire.Endpoint)
                ? TelemetryDefaults.AspireOtlpEndpoint
                : options.Aspire.Endpoint;
            endpoints.Add(aspireEndpoint);
        }

        if (
            IsOtlpExporterEnabled(options, environment)
            && !string.IsNullOrWhiteSpace(options.Otlp.Endpoint)
        )
        {
            endpoints.Add(options.Otlp.Endpoint);
        }

        return endpoints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static bool IsAspireExporterEnabled(
        ObservabilityOptions options,
        IHostEnvironment environment
    ) =>
        options.Exporters.Aspire.Enabled
        ?? (environment.IsDevelopment() && !IsRunningInContainer());

    internal static bool IsOtlpExporterEnabled(
        ObservabilityOptions options,
        IHostEnvironment environment
    ) => options.Exporters.Otlp.Enabled ?? IsRunningInContainer();

    internal static bool IsConsoleExporterEnabled(ObservabilityOptions options) =>
        options.Exporters.Console.Enabled ?? false;

    private static bool IsRunningInContainer() =>
        string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );

    internal static ObservabilityOptions GetObservabilityOptions(IConfiguration configuration) =>
        configuration.SectionFor<ObservabilityOptions>().Get<ObservabilityOptions>() ?? new();

    internal static AppOptions GetAppOptions(IConfiguration configuration) =>
        configuration.SectionFor<AppOptions>().Get<AppOptions>() ?? new();

    internal static Dictionary<string, object> BuildResourceAttributes(
        AppOptions appOptions,
        IHostEnvironment environment
    )
    {
        var serviceName = string.IsNullOrWhiteSpace(appOptions.ServiceName)
            ? ObservabilityConventions.ActivitySourceName
            : appOptions.ServiceName;
        var entryAssembly = Assembly.GetEntryAssembly();
        var assemblyName = entryAssembly?.GetName().Name ?? serviceName;
        var version = entryAssembly?.GetName().Version?.ToString() ?? TelemetryDefaults.Unknown;
        var machineName = Environment.MachineName;
        var processId = Environment.ProcessId;

        return new Dictionary<string, object>
        {
            [TelemetryResourceAttributeKeys.AssemblyName] = assemblyName,
            [TelemetryResourceAttributeKeys.ServiceName] = serviceName,
            [TelemetryResourceAttributeKeys.ServiceNamespace] = serviceName,
            [TelemetryResourceAttributeKeys.ServiceVersion] = version,
            [TelemetryResourceAttributeKeys.ServiceInstanceId] = $"{machineName}-{processId}",
            [TelemetryResourceAttributeKeys.DeploymentEnvironmentName] =
                environment.EnvironmentName,
            [TelemetryResourceAttributeKeys.HostName] = machineName,
            [TelemetryResourceAttributeKeys.HostArchitecture] =
                RuntimeInformation.OSArchitecture.ToString(),
            [TelemetryResourceAttributeKeys.OsType] = GetOsType(),
            [TelemetryResourceAttributeKeys.ProcessPid] = processId,
            [TelemetryResourceAttributeKeys.ProcessRuntimeName] = ".NET",
            [TelemetryResourceAttributeKeys.ProcessRuntimeVersion] = Environment.Version.ToString(),
        };
    }

    private static string GetOsType() =>
        OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsLinux() ? "linux"
        : OperatingSystem.IsMacOS() ? "darwin"
        : TelemetryDefaults.Unknown;

    private static void ConfigureTracingExporters(
        TracerProviderBuilder builder,
        IReadOnlyList<string> otlpEndpoints,
        bool enableConsoleExporter
    )
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
        bool enableConsoleExporter
    )
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
