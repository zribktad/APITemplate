namespace APITemplate.Application.Features.Category.DTOs;
public sealed record UpdateCategoryRequest(
    string Name,
    string? Description);
