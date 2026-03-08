using System.Diagnostics;
using Microsoft.AspNetCore.OutputCaching;
using APITemplate.Infrastructure.Observability;

namespace APITemplate.Api.Cache;

public sealed class OutputCacheInvalidationService : IOutputCacheInvalidationService
{
    private readonly IOutputCacheStore _outputCacheStore;

    public OutputCacheInvalidationService(IOutputCacheStore outputCacheStore)
    {
        _outputCacheStore = outputCacheStore;
    }

    public Task EvictAsync(string tag, CancellationToken cancellationToken = default)
        => EvictAsync([tag], cancellationToken);

    public async Task EvictAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        foreach (var tag in tags.Distinct(StringComparer.Ordinal))
        {
            var startedAt = Stopwatch.GetTimestamp();
            using var activity = CacheTelemetry.StartOutputCacheInvalidationActivity(tag);
            await _outputCacheStore.EvictByTagAsync(tag, cancellationToken);
            CacheTelemetry.RecordOutputCacheInvalidation(
                tag,
                Stopwatch.GetElapsedTime(startedAt));
        }
    }
}
