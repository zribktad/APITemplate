using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace APITemplate.Infrastructure.Observability;

public static class GraphQlTelemetry
{
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlRequests,
        description: "Total number of GraphQL requests observed.");

    private static readonly Counter<long> Errors = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlErrors,
        description: "Total number of GraphQL errors observed.");

    private static readonly Counter<long> DocumentCacheHits = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlDocumentCacheHits,
        description: "GraphQL document cache hits.");

    private static readonly Counter<long> DocumentCacheMisses = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlDocumentCacheMisses,
        description: "GraphQL document cache misses.");

    private static readonly Counter<long> OperationCacheHits = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlOperationCacheHits,
        description: "GraphQL operation cache hits.");

    private static readonly Counter<long> OperationCacheMisses = Meter.CreateCounter<long>(
        TelemetryMetricNames.GraphQlOperationCacheMisses,
        description: "GraphQL operation cache misses.");

    private static readonly Histogram<double> RequestDurationMs = Meter.CreateHistogram<double>(
        TelemetryMetricNames.GraphQlRequestDuration,
        unit: "ms",
        description: "GraphQL request execution duration.");

    private static readonly Histogram<double> OperationCost = Meter.CreateHistogram<double>(
        TelemetryMetricNames.GraphQlOperationCost,
        description: "Computed GraphQL operation cost.");

    public static void RecordRequest(string operationType, bool hasErrors, TimeSpan duration)
    {
        var tags = new TagList
        {
            { TelemetryTagKeys.GraphQlOperationType, operationType },
            { TelemetryTagKeys.GraphQlHasErrors, hasErrors }
        };

        Requests.Add(1, tags);
        RequestDurationMs.Record(duration.TotalMilliseconds, tags);
    }

    public static void RecordRequestError() => RecordError(TelemetryGraphQlValues.RequestPhase);

    public static void RecordSyntaxError() => RecordError(TelemetryGraphQlValues.SyntaxPhase);

    public static void RecordValidationError() => RecordError(TelemetryGraphQlValues.ValidationPhase);

    public static void RecordResolverError() => RecordError(TelemetryGraphQlValues.ResolverPhase);

    public static void RecordDocumentCacheHit() => DocumentCacheHits.Add(1);

    public static void RecordDocumentCacheMiss() => DocumentCacheMisses.Add(1);

    public static void RecordOperationCacheHit() => OperationCacheHits.Add(1);

    public static void RecordOperationCacheMiss() => OperationCacheMisses.Add(1);

    public static void RecordOperationCost(double fieldCost, double typeCost)
    {
        OperationCost.Record(fieldCost, new TagList
        {
            { TelemetryTagKeys.GraphQlCostKind, TelemetryGraphQlValues.FieldCostKind }
        });
        OperationCost.Record(typeCost, new TagList
        {
            { TelemetryTagKeys.GraphQlCostKind, TelemetryGraphQlValues.TypeCostKind }
        });
    }

    private static void RecordError(string phase)
    {
        Errors.Add(1,
        [
            new KeyValuePair<string, object?>(TelemetryTagKeys.GraphQlPhase, phase)
        ]);
    }
}
