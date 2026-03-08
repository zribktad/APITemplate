using APITemplate.Application.Options;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Logging;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using System.ComponentModel.DataAnnotations;

namespace APITemplate.Extensions;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddApplicationRedaction(this WebApplicationBuilder builder)
    {
        builder.Services.AddRedaction(redactionBuilder =>
        {
            redactionBuilder.SetRedactor<ErasingRedactor>(LogDataClassifications.Personal);

#pragma warning disable EXTEXP0002 // HMAC redactor API is currently marked experimental in the Microsoft.Extensions.Compliance.Redaction package.
            redactionBuilder.SetHmacRedactor(
                options =>
                {
                    var redactionOptions = builder.Configuration
                        .GetSection("Redaction")
                        .Get<RedactionOptions>() ?? new RedactionOptions();
                    Validator.ValidateObject(
                        redactionOptions,
                        new ValidationContext(redactionOptions),
                        validateAllProperties: true);

                    var hmacKey = RedactionConfiguration.ResolveHmacKey(
                        redactionOptions,
                        Environment.GetEnvironmentVariable);

                    options.KeyId = redactionOptions.KeyId;
                    options.Key = hmacKey;
                },
                new DataClassificationSet(LogDataClassifications.Sensitive));
#pragma warning restore EXTEXP0002

            redactionBuilder.SetFallbackRedactor<ErasingRedactor>();
        });

        builder.Logging.EnableRedaction();

        return builder;
    }

    public static LoggerConfiguration AddOpenTelemetrySinks(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        loggerConfiguration.Enrich.With<ActivityTraceEnricher>();

        var options = configuration
            .GetSection(ObservabilityServiceCollectionExtensions.ObservabilitySectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        var serviceName = string.IsNullOrWhiteSpace(options.ServiceName)
            ? "APITemplate"
            : options.ServiceName;
        var endpoints = ObservabilityServiceCollectionExtensions.GetEnabledOtlpEndpoints(options, environment);

        foreach (var endpoint in endpoints)
        {
            loggerConfiguration.WriteTo.OpenTelemetry(otel =>
            {
                otel.Endpoint = endpoint;
                otel.Protocol = OtlpProtocol.Grpc;
                otel.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                    ["deployment.environment.name"] = environment.EnvironmentName,
                    ["service.instance.id"] = Environment.MachineName
                };
            });
        }

        return loggerConfiguration;
    }
}
