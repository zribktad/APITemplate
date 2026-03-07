namespace APITemplate.Application.Common.Security;

/// <summary>
/// Shared constants for authentication, OpenID Connect, and OAuth2 token payload names.
/// </summary>
public static class AuthConstants
{
    public static class HttpClients
    {
        public const string KeycloakToken = "KeycloakTokenClient";
    }

    public static class OpenIdConnect
    {
        public const string AuthorizationEndpointPath = "protocol/openid-connect/auth";
        public const string TokenEndpointPath = "protocol/openid-connect/token";
    }

    public static class OpenApi
    {
        public const string OAuth2Scheme = "OAuth2";
        public const string ScalarClientId = "api-template-scalar";
    }

    public static class Scopes
    {
        public const string OpenId = "openid";
        public const string Profile = "profile";
        public const string Email = "email";

        public static readonly string[] Default = [OpenId, Profile, Email];
    }

    public static class CookieTokenNames
    {
        public const string AccessToken = "access_token";
        public const string RefreshToken = "refresh_token";
        public const string ExpiresAt = "expires_at";
        public const string ExpiresIn = "expires_in";
    }

    public static class OAuth2FormParameters
    {
        public const string GrantType = "grant_type";
        public const string ClientId = "client_id";
        public const string ClientSecret = "client_secret";
        public const string RefreshToken = "refresh_token";
    }

    public static class OAuth2GrantTypes
    {
        public const string RefreshToken = "refresh_token";
    }

    public static class Claims
    {
        public const string RealmAccess = "realm_access";
        public const string Roles = "roles";
        public const string PreferredUsername = "preferred_username";
        public const string ServiceAccountUsernamePrefix = "service-account-";
    }
}