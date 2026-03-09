using APITemplate.Domain.Enums;

namespace APITemplate.Application.Features.User.DTOs;

public sealed record ChangeUserRoleRequest(UserRole Role);
