using System.Security.Claims;
using APITemplate.Application.Common.Security;
using Microsoft.Extensions.Logging;
using JwtTokenValidatedContext = Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext;
using OidcTokenValidatedContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext;

namespace APITemplate.Infrastructure.Security;

public static class TenantClaimValidator
{
    public static Task OnTokenValidated(JwtTokenValidatedContext context)
    {
        var identity = context.Principal?.Identity as ClaimsIdentity;
        if (identity != null)
            KeycloakClaimMapper.MapKeycloakClaims(identity);

        if (!HasValidTenantClaim(context.Principal) && !IsServiceAccount(context.Principal))
        {
            context.Fail("Missing required tenant_id claim.");
        }

        LogTokenValidated(context.HttpContext, context.Principal, "JwtBearer");

        return Task.CompletedTask;
    }

    public static Task OnTokenValidated(OidcTokenValidatedContext context)
    {
        var identity = context.Principal?.Identity as ClaimsIdentity;
        if (identity != null)
            KeycloakClaimMapper.MapKeycloakClaims(identity);

        if (!HasValidTenantClaim(context.Principal) && !IsServiceAccount(context.Principal))
        {
            context.Fail("Missing required tenant_id claim.");
        }

        LogTokenValidated(context.HttpContext, context.Principal, "OIDC");

        return Task.CompletedTask;
    }

    public static bool HasValidTenantClaim(ClaimsPrincipal? principal)
    {
        return principal?.HasClaim(
            c => c.Type == CustomClaimTypes.TenantId
                 && Guid.TryParse(c.Value, out var tenantId)
                 && tenantId != Guid.Empty) == true;
    }

    private static bool IsServiceAccount(ClaimsPrincipal? principal)
    {
        var username = principal?.FindFirstValue("preferred_username");
        return username != null && username.StartsWith("service-account-", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogTokenValidated(HttpContext httpContext, ClaimsPrincipal? principal, string scheme)
    {
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(TenantClaimValidator));

        if (principal?.Identity is not ClaimsIdentity identity)
        {
            logger.LogWarning("[{Scheme}] Token validated but no identity found", scheme);
            return;
        }

        var claims = identity.Claims
            .Select(c => new { c.Type, c.Value })
            .ToList();

        logger.LogDebug("[{Scheme}] Token validated with {ClaimCount} claims: {@Claims}",
            scheme, claims.Count, claims);

        var name = identity.FindFirst(ClaimTypes.Name)?.Value;
        var roles = identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var tenantId = identity.FindFirst(CustomClaimTypes.TenantId)?.Value;

        logger.LogInformation(
            "[{Scheme}] Authenticated user={User}, tenant={TenantId}, roles=[{Roles}]",
            scheme, name, tenantId, string.Join(", ", roles));
    }
}
