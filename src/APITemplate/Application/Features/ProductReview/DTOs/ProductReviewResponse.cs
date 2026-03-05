namespace APITemplate.Application.Features.ProductReview.DTOs;
public sealed record ProductReviewResponse(
    Guid Id,
    Guid ProductId,
    Guid UserId,
    string? Comment,
    int Rating,
    DateTime CreatedAtUtc);
