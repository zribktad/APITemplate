using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

public sealed class ProductCategoryFacetSpecification : Specification<ProductEntity>
{
    public ProductCategoryFacetSpecification(ProductFilter filter)
    {
        ProductFilterCriteria.Apply(
            Query,
            filter,
            new ProductFilterCriteriaOptions(IgnoreCategoryIds: true));

        Query.AsNoTracking();
    }
}
