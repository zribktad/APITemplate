namespace APITemplate.Application.DTOs;

public sealed record CreateProductRequest(string Name, string? Description, decimal Price);
