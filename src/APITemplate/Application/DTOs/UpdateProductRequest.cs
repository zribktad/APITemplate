namespace APITemplate.Application.DTOs;

public sealed record UpdateProductRequest(string Name, string? Description, decimal Price);
