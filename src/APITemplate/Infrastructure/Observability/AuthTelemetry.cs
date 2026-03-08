using System.Diagnostics;
using System.Diagnostics.Metrics;
using APITemplate.Application.Common.Security;

namespace APITemplate.Infrastructure.Observability;

public static class AuthTelemetry
{
    private static readonly ActivitySource ActivitySource = new(ObservabilityConventions.ActivitySourceName);
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> AuthFailures = Meter.CreateCounter<long>(
        TelemetryMetricNames.AuthFailures,
        description: "Authentication and BFF session failures grouped by scheme and reason.");

    public static void RecordMissingTenantClaim(HttpContext httpContext, string scheme)
        => RecordFailure(TelemetryActivityNames.TokenValidated, scheme, TelemetryFailureReasons.MissingTenantClaim, ResolveSurface(httpContext.Request.Path));

    public static void RecordCookieRefreshFailed(Exception? exception = null)
        => RecordFailure(TelemetryActivityNames.CookieSessionRefresh, BffAuthenticationSchemes.Cookie, TelemetryFailureReasons.RefreshFailed, TelemetrySurfaces.Bff, exception);

    public static void RecordMissingRefreshToken()
        => RecordFailure(TelemetryActivityNames.CookieSessionRefresh, BffAuthenticationSchemes.Cookie, TelemetryFailureReasons.MissingRefreshToken, TelemetrySurfaces.Bff);

    public static void RecordTokenEndpointRejected()
        => RecordFailure(TelemetryActivityNames.CookieSessionRefresh, BffAuthenticationSchemes.Cookie, TelemetryFailureReasons.TokenEndpointRejected, TelemetrySurfaces.Bff);

    public static void RecordTokenRefreshException(Exception exception)
        => RecordFailure(TelemetryActivityNames.CookieSessionRefresh, BffAuthenticationSchemes.Cookie, TelemetryFailureReasons.TokenRefreshException, TelemetrySurfaces.Bff, exception);

    public static void RecordUnauthorizedRedirect()
        => RecordFailure(TelemetryActivityNames.RedirectToLogin, BffAuthenticationSchemes.Cookie, TelemetryFailureReasons.UnauthorizedRedirect, TelemetrySurfaces.Bff);

    private static void RecordFailure(string activityName, string scheme, string reason, string surface, Exception? exception = null)
    {
        AuthFailures.Add(1,
        [
            new KeyValuePair<string, object?>(TelemetryTagKeys.AuthScheme, scheme),
            new KeyValuePair<string, object?>(TelemetryTagKeys.AuthFailureReason, reason),
            new KeyValuePair<string, object?>(TelemetryTagKeys.ApiSurface, surface)
        ]);

        using var activity = ActivitySource.StartActivity(activityName, ActivityKind.Internal);
        activity?.SetTag(TelemetryTagKeys.AuthScheme, scheme);
        activity?.SetTag(TelemetryTagKeys.AuthFailureReason, reason);
        activity?.SetTag(TelemetryTagKeys.ApiSurface, surface);
        activity?.SetStatus(ActivityStatusCode.Error);
        if (exception is not null)
            activity?.SetTag(TelemetryTagKeys.ExceptionType, exception.GetType().Name);
    }

    private static string ResolveSurface(PathString path)
        => TelemetryApiSurfaceResolver.Resolve(path);
}
