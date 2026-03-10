using APITemplate.Api.GraphQL.DataLoaders;

namespace APITemplate.Api.GraphQL.Types;

public sealed class ProductTypeResolvers
{
    public async Task<ProductReviewResponse[]> GetReviews(
        [Parent] ProductResponse product,
        ProductReviewsByProductDataLoader loader,
        CancellationToken ct)
        => await loader.LoadAsync(product.Id, ct) ?? Array.Empty<ProductReviewResponse>();
}
