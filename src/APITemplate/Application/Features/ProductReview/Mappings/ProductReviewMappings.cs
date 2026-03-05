using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Mappings;
public static class ProductReviewMappings
{
    public static ProductReviewResponse ToResponse(this ProductReviewEntity review) =>
        new(review.Id, review.ProductId, review.UserId, review.Comment, review.Rating, review.Audit.CreatedAtUtc);
}
