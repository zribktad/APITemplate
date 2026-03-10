using APITemplate.Api.Cache;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IOutputCacheInvalidationService _outputCacheInvalidationService;

    public ProductsController(
        ISender sender,
        IOutputCacheInvalidationService outputCacheInvalidationService)
    {
        _sender = sender;
        _outputCacheInvalidationService = outputCacheInvalidationService;
    }

    [HttpGet]
    [OutputCache(PolicyName = CachePolicyNames.Products)]
    public async Task<ActionResult<ProductsResponse>> GetAll([FromQuery] ProductFilter filter, CancellationToken ct)
    {
        var products = await _sender.Send(new GetProductsQuery(filter), ct);
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = CachePolicyNames.Products)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        var product = await _sender.Send(new GetProductByIdQuery(id), ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = await _sender.Send(new CreateProductCommand(request), ct);
        await _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Products, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id, version = "1.0" }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdateProductCommand(id, request), ct);
        await _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Products, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteProductCommand(id), ct);
        await _outputCacheInvalidationService.EvictAsync(
            [CachePolicyNames.Products, CachePolicyNames.Reviews],
            ct);
        return NoContent();
    }
}
