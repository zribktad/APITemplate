using APITemplate.Api.Cache;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly IOutputCacheInvalidationService _outputCacheInvalidationService;

    public CategoriesController(
        ICategoryService categoryService,
        IOutputCacheInvalidationService outputCacheInvalidationService)
    {
        _categoryService = categoryService;
        _outputCacheInvalidationService = outputCacheInvalidationService;
    }

    [HttpGet]
    [OutputCache(PolicyName = CachePolicyNames.Categories)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken ct)
    {
        var categories = await _categoryService.GetAllAsync(ct);
        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = CachePolicyNames.Categories)]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken ct)
    {
        var category = await _categoryService.GetByIdAsync(id, ct);
        return category is null ? NotFound() : Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        var category = await _categoryService.CreateAsync(request, ct);
        await _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Categories, ct);
        return CreatedAtAction(nameof(GetById), new { id = category.Id, version = "1.0" }, category);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        await _categoryService.UpdateAsync(id, request, ct);
        await _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Categories, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _categoryService.DeleteAsync(id, ct);
        await _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Categories, ct);
        return NoContent();
    }

    /// <summary>
    /// Returns aggregated statistics for a category by calling the
    /// <c>get_product_category_stats(p_category_id)</c> stored procedure via EF Core FromSql.
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    [OutputCache(PolicyName = CachePolicyNames.Categories)]
    public async Task<ActionResult<ProductCategoryStatsResponse>> GetStats(Guid id, CancellationToken ct)
    {
        var stats = await _categoryService.GetStatsAsync(id, ct);
        return stats is null ? NotFound() : Ok(stats);
    }
}
