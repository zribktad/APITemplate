using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

public sealed class ProductByIdWithLinksSpecification : Specification<ProductEntity>
{
    public ProductByIdWithLinksSpecification(Guid id)
    {
        Query.Where(product => product.Id == id)
            .Include(product => product.ProductDataLinks);
    }
}
