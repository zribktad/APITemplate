using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;
public sealed class ProductSpecification : Specification<ProductEntity, ProductResponse>
{
    public ProductSpecification(ProductFilter filter)
    {
        ProductFilterCriteria.Apply(Query, filter);

        Query.OrderByDescending(p => p.CreatedAt)
             .Select(p => new ProductResponse(p.Id, p.Name, p.Description, p.Price, p.CreatedAt));

        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }
}
