namespace APITemplate.Application.Features.Tenant.DTOs;

public sealed record TenantResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc
);
