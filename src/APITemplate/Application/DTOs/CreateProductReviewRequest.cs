namespace APITemplate.Application.DTOs;

public sealed record CreateProductReviewRequest(
    Guid ProductId,
    string ReviewerName,
    string? Comment,
    int Rating);
