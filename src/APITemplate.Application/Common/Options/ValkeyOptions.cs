using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options;

public sealed class ValkeyOptions
{
    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}
