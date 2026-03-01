using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Mappings;

public static class ProductReviewMappings
{
    public static ProductReviewResponse ToResponse(this ProductReview review) =>
        new(review.Id, review.ProductId, review.ReviewerName, review.Comment, review.Rating, review.CreatedAt);
}
