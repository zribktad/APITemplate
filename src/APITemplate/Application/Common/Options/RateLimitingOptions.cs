using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options;

public sealed class RateLimitingOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; } = 100;

    [Range(1, int.MaxValue)]
    public int WindowMinutes { get; init; } = 1;
}
