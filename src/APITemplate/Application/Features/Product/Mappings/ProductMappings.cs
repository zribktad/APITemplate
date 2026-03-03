using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Mappings;
public static class ProductMappings
{
    public static ProductResponse ToResponse(this ProductEntity product) =>
        new(product.Id, product.Name, product.Description, product.Price, product.CreatedAt);
}
