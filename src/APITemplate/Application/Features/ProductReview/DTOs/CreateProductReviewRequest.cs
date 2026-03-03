namespace APITemplate.Application.Features.ProductReview.DTOs;
public sealed record CreateProductReviewRequest(
    Guid ProductId,
    string ReviewerName,
    string? Comment,
    int Rating);
