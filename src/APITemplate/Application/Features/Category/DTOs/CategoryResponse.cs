namespace APITemplate.Application.Features.Category.DTOs;
public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt);
