using Microsoft.AspNetCore.Http;

namespace APITemplate.Infrastructure.Observability;

public static class TelemetryApiSurfaceResolver
{
    public static string Resolve(PathString path)
    {
        if (path.StartsWithSegments(TelemetryPathPrefixes.GraphQl))
            return TelemetrySurfaces.GraphQl;

        if (path.StartsWithSegments(TelemetryPathPrefixes.Health))
            return TelemetrySurfaces.Health;

        if (path.StartsWithSegments(TelemetryPathPrefixes.Scalar)
            || path.StartsWithSegments(TelemetryPathPrefixes.OpenApi))
        {
            return TelemetrySurfaces.Documentation;
        }

        return TelemetrySurfaces.Rest;
    }
}
