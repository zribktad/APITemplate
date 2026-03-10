using APITemplate.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace APITemplate.Infrastructure.Health;

public sealed class MongoDbHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private readonly MongoDbContext _context;

    public MongoDbHealthCheck(MongoDbContext context) => _context = context;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CheckTimeout);
            await _context.PingAsync(cts.Token);
            return HealthCheckResult.Healthy();
        }
        catch
        {
            return HealthCheckResult.Unhealthy("MongoDB is not reachable");
        }
    }
}
