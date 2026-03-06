namespace APITemplate.Application.Common.Options;

public sealed class BffOptions
{
    public string CookieName { get; init; } = ".APITemplate.Auth";
    public string PostLogoutRedirectUri { get; init; } = "/";
    public int SessionTimeoutMinutes { get; init; } = 60;
    public string[] Scopes { get; init; } = ["openid", "profile", "email"];
}
