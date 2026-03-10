using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category.Specifications;

internal static class CategoryFilterCriteria
{
    private const string SearchConfiguration = "english";

    internal static void Apply(ISpecificationBuilder<CategoryEntity> query, CategoryFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Query))
            return;

        query.Where(category =>
            EF.Functions
                .ToTsVector(SearchConfiguration, category.Name + " " + (category.Description ?? string.Empty))
                .Matches(EF.Functions.WebSearchToTsQuery(SearchConfiguration, filter.Query)));
    }
}
