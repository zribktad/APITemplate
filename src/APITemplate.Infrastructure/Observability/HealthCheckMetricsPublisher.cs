using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace APITemplate.Infrastructure.Observability;

public sealed class HealthCheckMetricsPublisher : IHealthCheckPublisher
{
    private static readonly Meter Meter = new(ObservabilityConventions.HealthMeterName);
    private static readonly ConcurrentDictionary<string, int> Statuses = new(StringComparer.OrdinalIgnoreCase);

    private readonly ObservableGauge<int> _gauge;

    public HealthCheckMetricsPublisher()
    {
        _gauge = Meter.CreateObservableGauge(
            TelemetryMetricNames.HealthStatus,
            ObserveStatuses,
            unit: null,
            description: "Current health check status where 1=healthy and 0=unhealthy/degraded.");
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var entry in report.Entries)
        {
            Statuses[entry.Key] = entry.Value.Status == HealthStatus.Healthy ? 1 : 0;
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<Measurement<int>> ObserveStatuses()
    {
        foreach (var status in Statuses)
        {
            yield return new Measurement<int>(
                status.Value,
                new KeyValuePair<string, object?>(TelemetryTagKeys.Service, status.Key));
        }
    }
}
