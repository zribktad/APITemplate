using APITemplate.Application.Features.Category.Mappings;
using Ardalis.Specification;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category.Specifications;

public sealed class CategoryByIdSpecification : Specification<CategoryEntity, CategoryResponse>
{
    public CategoryByIdSpecification(Guid id)
    {
        Query.Where(category => category.Id == id)
            .AsNoTracking()
            .Select(CategoryMappings.Projection);
    }
}
