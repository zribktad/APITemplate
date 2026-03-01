using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;
using HotChocolate.Authorization;
using HotChocolate.Types;

namespace APITemplate.Api.GraphQL.Mutations;

[Authorize]
[ExtendObjectType(typeof(ProductMutations))]
public class ProductReviewMutations
{
    public async Task<ProductReviewResponse> CreateProductReview(
        CreateProductReviewRequest input,
        [Service] IProductReviewService reviewService,
        CancellationToken ct)
    {
        return await reviewService.CreateAsync(input, ct);
    }

    public async Task<bool> DeleteProductReview(
        Guid id,
        [Service] IProductReviewService reviewService,
        CancellationToken ct)
    {
        await reviewService.DeleteAsync(id, ct);
        return true;
    }
}
