using System.Linq.Expressions;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Mappings;
public static class ProductMappings
{
    public static readonly Expression<Func<ProductEntity, ProductResponse>> Projection =
        p => new ProductResponse(
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.Audit.CreatedAtUtc,
            p.ProductDataLinks.Select(link => link.ProductDataId).ToArray());

    private static readonly Func<ProductEntity, ProductResponse> CompiledProjection = Projection.Compile();

    public static ProductResponse ToResponse(this ProductEntity product) => CompiledProjection(product);
}
