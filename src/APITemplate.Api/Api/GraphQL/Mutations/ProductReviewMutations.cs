using HotChocolate.Authorization;
using MediatR;
using APITemplate.Api.Cache;

namespace APITemplate.Api.GraphQL.Mutations;

[Authorize]
[ExtendObjectType(typeof(ProductMutations))]
public class ProductReviewMutations
{
    public async Task<ProductReviewResponse> CreateProductReview(
        CreateProductReviewRequest input,
        [Service] ISender sender,
        [Service] IOutputCacheInvalidationService outputCacheInvalidationService,
        CancellationToken ct)
    {
        var review = await sender.Send(new CreateProductReviewCommand(input), ct);
        await outputCacheInvalidationService.EvictAsync(CachePolicyNames.Reviews, ct);
        return review;
    }

    public async Task<bool> DeleteProductReview(
        Guid id,
        [Service] ISender sender,
        [Service] IOutputCacheInvalidationService outputCacheInvalidationService,
        CancellationToken ct)
    {
        await sender.Send(new DeleteProductReviewCommand(id), ct);
        await outputCacheInvalidationService.EvictAsync(CachePolicyNames.Reviews, ct);
        return true;
    }
}
