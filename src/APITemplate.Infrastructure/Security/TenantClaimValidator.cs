using System.Security.Claims;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JwtTokenValidatedContext = Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext;
using OidcTokenValidatedContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext;

namespace APITemplate.Infrastructure.Security;

/// <summary>
/// Validates tenant-related claims after JWT/OIDC token validation and normalizes
/// Keycloak claims into standard .NET claim types used by authorization policies.
/// </summary>
public static class TenantClaimValidator
{
    /// <summary>
    /// JWT Bearer token callback executed after signature and lifetime checks.
    /// Maps Keycloak claims, enforces tenant claim presence for user tokens, and logs
    /// authentication details for diagnostics.
    /// </summary>
    /// <param name="context">The JWT token validation context.</param>
    /// <returns>A completed task.</returns>
    public static Task OnTokenValidated(JwtTokenValidatedContext context)
    {
        var identity = context.Principal?.Identity as ClaimsIdentity;
        if (identity != null)
            KeycloakClaimMapper.MapKeycloakClaims(identity);

        if (!HasValidTenantClaim(context.Principal) && !IsServiceAccount(context.Principal))
        {
            AuthTelemetry.RecordMissingTenantClaim(context.HttpContext, JwtBearerDefaults.AuthenticationScheme);
            context.Fail($"Missing required {CustomClaimTypes.TenantId} claim.");
        }

        LogTokenValidated(context.HttpContext, context.Principal, JwtBearerDefaults.AuthenticationScheme);

        return Task.CompletedTask;
    }

    /// <summary>
    /// OpenID Connect token callback executed after token validation in BFF login flow.
    /// Applies the same tenant and claim-mapping rules as JWT Bearer validation.
    /// </summary>
    /// <param name="context">The OIDC token validation context.</param>
    /// <returns>A completed task.</returns>
    public static Task OnTokenValidated(OidcTokenValidatedContext context)
    {
        var identity = context.Principal?.Identity as ClaimsIdentity;
        if (identity != null)
            KeycloakClaimMapper.MapKeycloakClaims(identity);

        if (!HasValidTenantClaim(context.Principal) && !IsServiceAccount(context.Principal))
        {
            AuthTelemetry.RecordMissingTenantClaim(context.HttpContext, OpenIdConnectDefaults.AuthenticationScheme);
            context.Fail($"Missing required {CustomClaimTypes.TenantId} claim.");
        }

        LogTokenValidated(context.HttpContext, context.Principal, OpenIdConnectDefaults.AuthenticationScheme);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks whether the principal has a non-empty GUID value in the <c>tenant_id</c> claim.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <returns><c>true</c> when a valid tenant claim exists; otherwise <c>false</c>.</returns>
    public static bool HasValidTenantClaim(ClaimsPrincipal? principal)
    {
        return principal?.HasClaim(
            c => c.Type == CustomClaimTypes.TenantId
                 && Guid.TryParse(c.Value, out var tenantId)
                 && tenantId != Guid.Empty) == true;
    }

    private static bool IsServiceAccount(ClaimsPrincipal? principal)
    {
        var username = principal?.FindFirstValue(AuthConstants.Claims.PreferredUsername);
        return username != null && username.StartsWith(AuthConstants.Claims.ServiceAccountUsernamePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void LogTokenValidated(HttpContext httpContext, ClaimsPrincipal? principal, string scheme)
    {
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(TenantClaimValidator));

        if (principal?.Identity is not ClaimsIdentity identity)
        {
            logger.TokenValidatedNoIdentity(scheme);
            return;
        }

        var claims = identity.Claims
            .Select(c => $"{c.Type}={c.Value}")
            .ToList();

        logger.TokenValidatedWithClaims(scheme, claims.Count, string.Join("; ", claims));

        var name = identity.FindFirst(ClaimTypes.Name)?.Value;
        var roles = identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var tenantId = identity.FindFirst(CustomClaimTypes.TenantId)?.Value;

        logger.UserAuthenticated(scheme, name, tenantId, string.Join(", ", roles));
    }
}
