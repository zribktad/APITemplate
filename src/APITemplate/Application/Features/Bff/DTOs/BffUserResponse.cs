namespace APITemplate.Application.Features.Bff.DTOs;

public sealed record BffUserResponse(string? UserId, string? Username, string? Email, string? TenantId, string[] Roles);
