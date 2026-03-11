using APITemplate.Application.Common.Security;
using HotChocolate.Authorization;
using MediatR;

namespace APITemplate.Api.GraphQL.Mutations;

[Authorize]
public class ProductMutations
{
    [Authorize(Policy = Permission.Products.Create)]
    public async Task<ProductResponse> CreateProduct(
        CreateProductRequest input,
        [Service] ISender sender,
        CancellationToken ct)
    {
        return await sender.Send(new CreateProductCommand(input), ct);
    }

    [Authorize(Policy = Permission.Products.Delete)]
    public async Task<bool> DeleteProduct(
        Guid id,
        [Service] ISender sender,
        CancellationToken ct)
    {
        await sender.Send(new DeleteProductCommand(id), ct);
        return true;
    }
}
