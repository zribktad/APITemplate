using APITemplate.Application.Common.Validation;
using FluentValidation;
using HotChocolate.Authorization;

namespace APITemplate.Api.GraphQL.Mutations;

[Authorize]
[ExtendObjectType(typeof(ProductMutations))]
public class ProductReviewMutations
{
    public async Task<ProductReviewResponse> CreateProductReview(
        CreateProductReviewRequest input,
        [Service] IProductReviewService reviewService,
        [Service] IValidator<CreateProductReviewRequest> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAppAsync(input, ct);
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
