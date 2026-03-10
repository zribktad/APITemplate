using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options;

public sealed class RateLimitingOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int WindowMinutes { get; set; } = 1;
}
