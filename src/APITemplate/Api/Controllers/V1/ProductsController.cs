using APITemplate.Api.Cache;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IOutputCacheStore _outputCacheStore;

    public ProductsController(IProductService productService, IOutputCacheStore outputCacheStore)
    {
        _productService = productService;
        _outputCacheStore = outputCacheStore;
    }

    [HttpGet]
    [OutputCache(PolicyName = CachePolicyNames.Products)]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> GetAll([FromQuery] ProductFilter filter, CancellationToken ct)
    {
        var products = await _productService.GetAllAsync(filter, ct);
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = CachePolicyNames.Products)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = await _productService.CreateAsync(request, ct);
        await _outputCacheStore.EvictByTagAsync(CachePolicyNames.Products, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id, version = "1.0" }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        await _productService.UpdateAsync(id, request, ct);
        await _outputCacheStore.EvictByTagAsync(CachePolicyNames.Products, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _productService.DeleteAsync(id, ct);
        await _outputCacheStore.EvictByTagAsync(CachePolicyNames.Products, ct);
        await _outputCacheStore.EvictByTagAsync(CachePolicyNames.Reviews, ct);
        return NoContent();
    }
}
