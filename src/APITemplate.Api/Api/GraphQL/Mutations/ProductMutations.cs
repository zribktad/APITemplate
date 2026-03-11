using HotChocolate.Authorization;
using MediatR;

namespace APITemplate.Api.GraphQL.Mutations;

[Authorize]
public class ProductMutations
{
    public async Task<ProductResponse> CreateProduct(
        CreateProductRequest input,
        [Service] ISender sender,
        CancellationToken ct)
    {
        return await sender.Send(new CreateProductCommand(input), ct);
    }

    public async Task<bool> DeleteProduct(
        Guid id,
        [Service] ISender sender,
        CancellationToken ct)
    {
        await sender.Send(new DeleteProductCommand(id), ct);
        return true;
    }
}
