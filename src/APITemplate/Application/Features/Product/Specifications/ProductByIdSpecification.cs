using APITemplate.Application.Features.Product.Mappings;
using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

public sealed class ProductByIdSpecification : Specification<ProductEntity, ProductResponse>
{
    public ProductByIdSpecification(Guid id)
    {
        Query.Where(product => product.Id == id)
            .Select(ProductMappings.Projection);
    }
}
