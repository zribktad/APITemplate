using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Options;

public sealed class RedactionOptions
{
    [Required]
    public string HmacKeyEnvironmentVariable { get; init; } = "APITEMPLATE_REDACTION_HMAC_KEY";

    public string HmacKey { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int KeyId { get; init; } = 1001;
}
