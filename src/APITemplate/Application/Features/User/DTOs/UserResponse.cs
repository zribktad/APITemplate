using APITemplate.Domain.Enums;

namespace APITemplate.Application.Features.User.DTOs;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    bool IsActive,
    UserRole Role,
    DateTime CreatedAtUtc);
