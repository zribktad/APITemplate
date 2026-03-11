using HotChocolate.Authorization;
using MediatR;

namespace APITemplate.Api.GraphQL.Mutations;

[Authorize]
[ExtendObjectType(typeof(ProductMutations))]
public class ProductReviewMutations
{
    public async Task<ProductReviewResponse> CreateProductReview(
        CreateProductReviewRequest input,
        [Service] ISender sender,
        CancellationToken ct)
    {
        return await sender.Send(new CreateProductReviewCommand(input), ct);
    }

    public async Task<bool> DeleteProductReview(
        Guid id,
        [Service] ISender sender,
        CancellationToken ct)
    {
        await sender.Send(new DeleteProductReviewCommand(id), ct);
        return true;
    }
}
