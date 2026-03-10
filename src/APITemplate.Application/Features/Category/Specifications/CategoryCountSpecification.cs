using Ardalis.Specification;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category.Specifications;

public sealed class CategoryCountSpecification : Specification<CategoryEntity>
{
    public CategoryCountSpecification(CategoryFilter filter)
    {
        CategoryFilterCriteria.Apply(Query, filter);
        Query.AsNoTracking();
    }
}
