using System.Linq.Expressions;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Mappings;
public static class ProductReviewMappings
{
    public static readonly Expression<Func<ProductReviewEntity, ProductReviewResponse>> Projection =
        r => new ProductReviewResponse(r.Id, r.ProductId, r.UserId, r.Comment, r.Rating, r.Audit.CreatedAtUtc);

    private static readonly Func<ProductReviewEntity, ProductReviewResponse> CompiledProjection = Projection.Compile();

    public static ProductReviewResponse ToResponse(this ProductReviewEntity review) => CompiledProjection(review);
}
