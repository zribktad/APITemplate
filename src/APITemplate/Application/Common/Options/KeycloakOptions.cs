using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace APITemplate.Application.Common.Options;

public sealed class KeycloakOptions
{
    [Required]
    [ConfigurationKeyName("realm")]
    public string Realm { get; init; } = string.Empty;

    [Required]
    [ConfigurationKeyName("auth-server-url")]
    public string AuthServerUrl { get; init; } = string.Empty;

    [ConfigurationKeyName("resource")]
    public string Resource { get; init; } = string.Empty;

    [ConfigurationKeyName("credentials")]
    public KeycloakCredentialsOptions Credentials { get; init; } = new();
}

public sealed class KeycloakCredentialsOptions
{
    [ConfigurationKeyName("secret")]
    public string Secret { get; init; } = string.Empty;
}
