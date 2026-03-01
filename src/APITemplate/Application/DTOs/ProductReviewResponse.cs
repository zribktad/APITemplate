namespace APITemplate.Application.DTOs;

public sealed record ProductReviewResponse(
    Guid Id,
    Guid ProductId,
    string ReviewerName,
    string? Comment,
    int Rating,
    DateTime CreatedAt);
