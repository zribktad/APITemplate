using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;
public sealed class ProductSpecification : Specification<ProductEntity, ProductResponse>
{
    public ProductSpecification(ProductFilter filter)
    {
        ProductFilterCriteria.Apply(Query, filter);

        ApplySorting(Query, filter);

        Query.Select(p => new ProductResponse(p.Id, p.Name, p.Description, p.Price, p.CreatedAt));

        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }

    private static void ApplySorting(ISpecificationBuilder<ProductEntity> query, ProductFilter filter)
    {
        var sortBy = filter.SortBy?.Trim().ToLowerInvariant();
        var desc = !string.Equals(filter.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        switch (sortBy)
        {
            case "name":
                if (desc) query.OrderByDescending(p => p.Name);
                else query.OrderBy(p => p.Name);
                break;
            case "price":
                if (desc) query.OrderByDescending(p => p.Price);
                else query.OrderBy(p => p.Price);
                break;
            default:
                if (desc) query.OrderByDescending(p => p.CreatedAt);
                else query.OrderBy(p => p.CreatedAt);
                break;
        }
    }
}
