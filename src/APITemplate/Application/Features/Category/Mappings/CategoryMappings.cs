using CategoryEntity = APITemplate.Domain.Entities.Category;
using ProductCategoryStatsEntity = APITemplate.Domain.Entities.ProductCategoryStats;

namespace APITemplate.Application.Features.Category.Mappings;
public static class CategoryMappings
{
    public static CategoryResponse ToResponse(this CategoryEntity category) =>
        new(category.Id, category.Name, category.Description, category.CreatedAt);

    public static ProductCategoryStatsResponse ToResponse(this ProductCategoryStatsEntity stats) =>
        new(stats.CategoryId, stats.CategoryName, stats.ProductCount, stats.AveragePrice, stats.TotalReviews);
}
