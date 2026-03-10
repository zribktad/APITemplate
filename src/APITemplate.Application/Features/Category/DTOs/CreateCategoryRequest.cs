namespace APITemplate.Application.Features.Category.DTOs;
public sealed record CreateCategoryRequest(
    string Name,
    string? Description);
