using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;

namespace APITemplate.Api.GraphQL.Queries;

public class ProductQueries
{
    public async Task<IReadOnlyList<ProductResponse>> GetProducts(
        [Service] IProductService productService,
        CancellationToken ct)
    {
        return await productService.GetAllAsync(ct);
    }

    public async Task<ProductResponse?> GetProductById(
        Guid id,
        [Service] IProductService productService,
        CancellationToken ct)
    {
        return await productService.GetByIdAsync(id, ct);
    }
}
