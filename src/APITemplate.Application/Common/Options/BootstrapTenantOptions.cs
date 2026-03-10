using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options;

public sealed class BootstrapTenantOptions
{
    [Required]
    public string Code { get; init; } = "default";

    [Required]
    public string Name { get; init; } = "Default Tenant";
}
