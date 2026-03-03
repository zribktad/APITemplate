using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options;
public sealed class AuthOptions
{
    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
