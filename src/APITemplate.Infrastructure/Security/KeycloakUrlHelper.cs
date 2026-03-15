using APITemplate.Application.Common.Security;

namespace APITemplate.Infrastructure.Security;

public static class KeycloakUrlHelper
{
    public static string BuildAuthority(string authServerUrl, string realm)
        => $"{authServerUrl.TrimEnd('/')}/realms/{realm}";

    public static string BuildDiscoveryUrl(string authServerUrl, string realm)
        => $"{BuildAuthority(authServerUrl, realm)}/.well-known/openid-configuration";

    public static string BuildTokenEndpoint(string authServerUrl, string realm)
        => $"{BuildAuthority(authServerUrl, realm)}/{AuthConstants.OpenIdConnect.TokenEndpointPath}";
}
