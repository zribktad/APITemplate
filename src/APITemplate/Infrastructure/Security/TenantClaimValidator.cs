using System.Security.Claims;
using APITemplate.Application.Common.Security;
using JwtTokenValidatedContext = Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext;
using OidcTokenValidatedContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext;

namespace APITemplate.Infrastructure.Security;

public static class TenantClaimValidator
{
    public static bool HasValidTenantClaim(ClaimsPrincipal? principal)
    {
        return principal?.HasClaim(
            c => c.Type == CustomClaimTypes.TenantId
                 && Guid.TryParse(c.Value, out var tenantId)
                 && tenantId != Guid.Empty) == true;
    }

    public static Task OnTokenValidated(JwtTokenValidatedContext context)
    {
        if (!HasValidTenantClaim(context.Principal))
        {
            context.Fail("Missing required tenant_id claim.");
        }

        return Task.CompletedTask;
    }

    public static Task OnTokenValidated(OidcTokenValidatedContext context)
    {
        if (!HasValidTenantClaim(context.Principal))
        {
            context.Fail("Missing required tenant_id claim.");
        }

        return Task.CompletedTask;
    }
}
