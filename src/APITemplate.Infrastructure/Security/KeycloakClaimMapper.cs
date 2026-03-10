using System.Security.Claims;
using System.Text.Json;
using APITemplate.Application.Common.Security;

namespace APITemplate.Infrastructure.Security;

public static class KeycloakClaimMapper
{
    public static void MapKeycloakClaims(ClaimsIdentity identity)
    {
        MapUsername(identity);
        MapRealmRoles(identity);
    }

    private static void MapUsername(ClaimsIdentity identity)
    {
        if (identity.FindFirst(ClaimTypes.Name) != null)
            return;

        var preferred = identity.FindFirst(AuthConstants.Claims.PreferredUsername);
        if (preferred != null)
            identity.AddClaim(new Claim(ClaimTypes.Name, preferred.Value));
    }

    private static void MapRealmRoles(ClaimsIdentity identity)
    {
        var realmAccess = identity.FindFirst(AuthConstants.Claims.RealmAccess);
        if (realmAccess == null)
            return;

        using var doc = JsonDocument.Parse(realmAccess.Value);
        if (!doc.RootElement.TryGetProperty(AuthConstants.Claims.Roles, out var roles))
            return;

        foreach (var role in roles.EnumerateArray())
        {
            var value = role.GetString();
            if (!string.IsNullOrEmpty(value))
                identity.AddClaim(new Claim(ClaimTypes.Role, value));
        }
    }
}
