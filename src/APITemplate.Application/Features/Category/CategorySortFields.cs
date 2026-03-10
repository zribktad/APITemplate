using APITemplate.Application.Common.Sorting;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category;

public static class CategorySortFields
{
    public static readonly SortField Name = new("name");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<CategoryEntity> Map = new SortFieldMap<CategoryEntity>()
        .Add(Name, c => c.Name)
        .Add(CreatedAt, c => c.Audit.CreatedAtUtc)
        .Default(c => c.Audit.CreatedAtUtc);
}
