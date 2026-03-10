using System.Linq.Expressions;
using CategoryEntity = APITemplate.Domain.Entities.Category;
using ProductCategoryStatsEntity = APITemplate.Domain.Entities.ProductCategoryStats;

namespace APITemplate.Application.Features.Category.Mappings;
public static class CategoryMappings
{
    public static readonly Expression<Func<CategoryEntity, CategoryResponse>> Projection =
        category => new CategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.Audit.CreatedAtUtc);

    private static readonly Func<CategoryEntity, CategoryResponse> CompiledProjection = Projection.Compile();

    public static CategoryResponse ToResponse(this CategoryEntity category) =>
        CompiledProjection(category);

    public static ProductCategoryStatsResponse ToResponse(this ProductCategoryStatsEntity stats) =>
        new(stats.CategoryId, stats.CategoryName, stats.ProductCount, stats.AveragePrice, stats.TotalReviews);
}
