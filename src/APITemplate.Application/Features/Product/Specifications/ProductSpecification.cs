using APITemplate.Application.Features.Product.Mappings;
using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;
public sealed class ProductSpecification : Specification<ProductEntity, ProductResponse>
{
    public ProductSpecification(ProductFilter filter)
    {
        ProductFilterCriteria.Apply(Query, filter);
        Query.AsNoTracking();

        ProductSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);

        Query.Select(ProductMappings.Projection);

        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }
}
