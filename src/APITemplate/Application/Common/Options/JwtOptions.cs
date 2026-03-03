using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options;
public sealed class JwtOptions
{
    [Required]
    public string Secret { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpirationMinutes { get; init; }
}
