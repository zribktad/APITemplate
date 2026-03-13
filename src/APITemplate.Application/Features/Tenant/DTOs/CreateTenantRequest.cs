using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Features.Tenant.DTOs;

public sealed record CreateTenantRequest(
    [Required, MaxLength(100)] string Code,
    [Required, MaxLength(200)] string Name
);
