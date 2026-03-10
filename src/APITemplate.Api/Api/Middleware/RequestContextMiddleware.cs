using System.Diagnostics;
using System.Security.Claims;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.Http.Features;
using Serilog.Context;

namespace APITemplate.Api.Middleware;

/// <summary>
/// Adds per-request context metadata used by logs and clients.
/// </summary>
/// <remarks>
/// In the current pipeline ordering this middleware runs after
/// <c>app.UseExceptionHandler()</c>, so thrown exceptions are still wrapped by
/// global exception handling while correlation and timing headers are maintained.
/// </remarks>
public sealed class RequestContextMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const string CorrelationIdItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        var stopwatch = Stopwatch.StartNew();
        var traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
        var tenantId = context.User.FindFirstValue(CustomClaimTypes.TenantId);
        var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId) ? tenantId : string.Empty;

        if (!string.IsNullOrWhiteSpace(effectiveTenantId))
        {
            Activity.Current?.SetTag(TelemetryTagKeys.TenantId, effectiveTenantId);
        }

        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.Response.Headers["X-Trace-Id"] = traceId;
        context.Response.Headers["X-Elapsed-Ms"] = "0";

        // Response headers are finalized here so elapsed time reflects full downstream execution.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Elapsed-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
            return Task.CompletedTask;
        });

        try
        {
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("TenantId", effectiveTenantId))
            {
                await _next(context);
            }
        }
        finally
        {
            var metricsTagsFeature = context.Features.Get<IHttpMetricsTagsFeature>();
            if (metricsTagsFeature is not null)
            {
                metricsTagsFeature.Tags.Add(new(TelemetryTagKeys.ApiSurface, TelemetryApiSurfaceResolver.Resolve(context.Request.Path)));
                metricsTagsFeature.Tags.Add(new(TelemetryTagKeys.Authenticated, context.User.Identity?.IsAuthenticated == true));
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationIdHeader].ToString();
        if (!string.IsNullOrWhiteSpace(incoming))
            return incoming;

        return context.TraceIdentifier;
    }

}
