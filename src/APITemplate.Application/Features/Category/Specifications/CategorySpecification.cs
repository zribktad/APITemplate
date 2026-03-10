using APITemplate.Application.Features.Category.Mappings;
using Ardalis.Specification;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category.Specifications;

public sealed class CategorySpecification : Specification<CategoryEntity, CategoryResponse>
{
    public CategorySpecification(CategoryFilter filter)
    {
        CategoryFilterCriteria.Apply(Query, filter);
        Query.AsNoTracking();
        CategorySortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);
        Query.Select(CategoryMappings.Projection);
        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize);
    }
}
