using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics.Metrics;

namespace APITemplate.Infrastructure.Observability;

public static class ValidationTelemetry
{
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> ValidationRequestsRejected = Meter.CreateCounter<long>(
        TelemetryMetricNames.ValidationRequestsRejected,
        description: "Number of requests rejected by validation.");

    private static readonly Counter<long> ValidationErrors = Meter.CreateCounter<long>(
        TelemetryMetricNames.ValidationErrors,
        description: "Number of individual validation errors.");

    public static void RecordValidationFailure(ActionExecutingContext context, Type argumentType, IEnumerable<ValidationFailure> failures)
    {
        var route = ResolveRoute(context);
        ValidationRequestsRejected.Add(1,
        [
            new KeyValuePair<string, object?>(TelemetryTagKeys.ValidationDtoType, argumentType.Name),
            new KeyValuePair<string, object?>(TelemetryTagKeys.HttpRoute, route)
        ]);

        foreach (var failure in failures)
        {
            ValidationErrors.Add(1,
            [
                new KeyValuePair<string, object?>(TelemetryTagKeys.ValidationDtoType, argumentType.Name),
                new KeyValuePair<string, object?>(TelemetryTagKeys.HttpRoute, route),
                new KeyValuePair<string, object?>(TelemetryTagKeys.ValidationProperty, failure.PropertyName)
            ]);
        }
    }

    private static string ResolveRoute(ActionExecutingContext context)
        => context.ActionDescriptor.AttributeRouteInfo?.Template
           ?? context.HttpContext.Request.Path.Value
           ?? TelemetryDefaults.Unknown;
}
