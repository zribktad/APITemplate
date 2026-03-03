using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;
public sealed class ProductCountSpecification : Specification<ProductEntity>
{
    public ProductCountSpecification(ProductFilter filter)
    {
        ProductFilterCriteria.Apply(Query, filter);
    }
}
