using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Infrastructure.Observability;

public static class CacheTelemetry
{
    private static readonly ActivitySource ActivitySource = new(ObservabilityConventions.ActivitySourceName);
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> OutputCacheInvalidations = Meter.CreateCounter<long>(
        TelemetryMetricNames.OutputCacheInvalidations,
        description: "Total number of output cache invalidation operations.");

    private static readonly Histogram<double> OutputCacheInvalidationDurationMs = Meter.CreateHistogram<double>(
        TelemetryMetricNames.OutputCacheInvalidationDuration,
        unit: "ms",
        description: "Duration of output cache invalidation operations.");

    private static readonly Counter<long> OutputCacheOutcomes = Meter.CreateCounter<long>(
        TelemetryMetricNames.OutputCacheOutcomes,
        description: "Observed output cache outcomes.");

    public static Activity? StartOutputCacheInvalidationActivity(string tag)
    {
        var activity = ActivitySource.StartActivity(TelemetryActivityNames.OutputCacheInvalidate, ActivityKind.Internal);
        activity?.SetTag(TelemetryTagKeys.CacheTag, tag);
        return activity;
    }

    public static void RecordOutputCacheInvalidation(string tag, TimeSpan duration)
    {
        var tags = new TagList
        {
            { TelemetryTagKeys.CacheTag, tag }
        };

        OutputCacheInvalidations.Add(1, tags);
        OutputCacheInvalidationDurationMs.Record(duration.TotalMilliseconds, tags);
    }

    public static void ConfigureRequest(OutputCacheContext context)
    {
        context.HttpContext.Items[TelemetryContextKeys.OutputCachePolicyName] = ResolvePolicyName(context);
    }

    public static void RecordCacheHit(OutputCacheContext context)
        => RecordCacheOutcome(context, TelemetryOutcomeValues.Hit);

    public static void RecordResponseOutcome(OutputCacheContext context)
    {
        var outcome = context.AllowCacheStorage
            ? TelemetryOutcomeValues.Store
            : TelemetryOutcomeValues.Bypass;
        RecordCacheOutcome(context, outcome);
    }

    private static void RecordCacheOutcome(OutputCacheContext context, string outcome)
    {
        OutputCacheOutcomes.Add(1,
        [
            new KeyValuePair<string, object?>(TelemetryTagKeys.CachePolicy, ResolvePolicyName(context)),
            new KeyValuePair<string, object?>(TelemetryTagKeys.ApiSurface, ResolveSurface(context.HttpContext.Request.Path)),
            new KeyValuePair<string, object?>(TelemetryTagKeys.CacheOutcome, outcome)
        ]);
    }

    private static string ResolvePolicyName(OutputCacheContext context)
    {
        if (context.HttpContext.Items.TryGetValue(TelemetryContextKeys.OutputCachePolicyName, out var cached) && cached is string name)
            return name;

        return context.HttpContext.GetEndpoint()?.Metadata
                   .OfType<OutputCacheAttribute>()
                   .Select(attribute => attribute.PolicyName)
                   .FirstOrDefault(policyName => !string.IsNullOrWhiteSpace(policyName))
               ?? TelemetryDefaults.Default;
    }

    private static string ResolveSurface(PathString path)
        => TelemetryApiSurfaceResolver.Resolve(path);
}
