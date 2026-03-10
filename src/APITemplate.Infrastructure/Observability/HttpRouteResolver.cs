using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace APITemplate.Infrastructure.Observability;

public static partial class HttpRouteResolver
{
    [GeneratedRegex(@"\{version(?::[^}]*)?\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionTokenRegex();

    public static string Resolve(HttpContext httpContext)
    {
        var routeTemplate = httpContext.GetEndpoint() is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText
            : null;

        if (string.IsNullOrWhiteSpace(routeTemplate))
            return httpContext.Request.Path.Value ?? TelemetryDefaults.Unknown;

        return ReplaceVersionToken(routeTemplate, httpContext.Request.RouteValues);
    }

    public static string ReplaceVersionToken(string routeTemplate, RouteValueDictionary routeValues)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate))
            return TelemetryDefaults.Unknown;

        if (!routeValues.TryGetValue("version", out var versionValue) || versionValue is null)
            return routeTemplate;

        var version = versionValue.ToString();
        if (string.IsNullOrWhiteSpace(version))
            return routeTemplate;

        return VersionTokenRegex().Replace(routeTemplate, version, 1);
    }
}
