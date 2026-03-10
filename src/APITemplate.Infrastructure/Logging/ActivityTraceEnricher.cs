using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace APITemplate.Infrastructure.Logging;

public sealed class ActivityTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
            return;

        if (activity.TraceId != default)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString()));
        }

        if (activity.SpanId != default)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString()));
        }
    }
}
