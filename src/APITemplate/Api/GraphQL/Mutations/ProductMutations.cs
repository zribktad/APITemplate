using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;

namespace APITemplate.Api.GraphQL.Mutations;

public class ProductMutations
{
    public async Task<ProductResponse> CreateProduct(
        CreateProductRequest input,
        [Service] IProductService productService,
        CancellationToken ct)
    {
        return await productService.CreateAsync(input, ct);
    }

    public async Task<bool> DeleteProduct(
        Guid id,
        [Service] IProductService productService,
        CancellationToken ct)
    {
        await productService.DeleteAsync(id, ct);
        return true;
    }
}
