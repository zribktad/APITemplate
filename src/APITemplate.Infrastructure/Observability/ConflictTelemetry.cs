using System.Diagnostics.Metrics;

namespace APITemplate.Infrastructure.Observability;

public static class ConflictTelemetry
{
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> ConcurrencyConflicts = Meter.CreateCounter<long>(
        TelemetryMetricNames.ConcurrencyConflicts,
        description: "Number of optimistic concurrency conflicts.");

    private static readonly Counter<long> DomainConflicts = Meter.CreateCounter<long>(
        TelemetryMetricNames.DomainConflicts,
        description: "Number of domain conflict responses.");

    public static void Record(Exception exception, string errorCode)
    {
        if (exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            ConcurrencyConflicts.Add(1);
            return;
        }

        if (exception is APITemplate.Domain.Exceptions.ConflictException)
        {
            DomainConflicts.Add(1,
            [
                new KeyValuePair<string, object?>(TelemetryTagKeys.ErrorCode, errorCode)
            ]);
        }
    }
}
