
namespace APITemplate.Application.Features.Category.Interfaces;
public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct = default);

    Task<CategoryResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns aggregated statistics for the category by calling the stored procedure
    /// <c>get_product_category_stats(p_category_id)</c> via EF Core <c>FromSql</c>.
    /// Returns <c>null</c> when the category does not exist.
    /// </summary>
    Task<ProductCategoryStatsResponse?> GetStatsAsync(Guid id, CancellationToken ct = default);
}
