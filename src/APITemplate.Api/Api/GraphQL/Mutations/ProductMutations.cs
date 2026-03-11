using HotChocolate.Authorization;
using MediatR;
using APITemplate.Api.Cache;

namespace APITemplate.Api.GraphQL.Mutations;

[Authorize]
public class ProductMutations
{
    public async Task<ProductResponse> CreateProduct(
        CreateProductRequest input,
        [Service] ISender sender,
        [Service] IOutputCacheInvalidationService outputCacheInvalidationService,
        CancellationToken ct)
    {
        var product = await sender.Send(new CreateProductCommand(input), ct);
        await outputCacheInvalidationService.EvictAsync(CachePolicyNames.Products, ct);
        return product;
    }

    public async Task<bool> DeleteProduct(
        Guid id,
        [Service] ISender sender,
        [Service] IOutputCacheInvalidationService outputCacheInvalidationService,
        CancellationToken ct)
    {
        await sender.Send(new DeleteProductCommand(id), ct);
        await outputCacheInvalidationService.EvictAsync(
            [CachePolicyNames.Products, CachePolicyNames.Reviews],
            ct);
        return true;
    }
}
