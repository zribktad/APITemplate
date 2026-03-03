namespace APITemplate.Application.Features.ProductReview.DTOs;
public sealed record ProductReviewResponse(
    Guid Id,
    Guid ProductId,
    string ReviewerName,
    string? Comment,
    int Rating,
    DateTime CreatedAt);
