using APITemplate.Api.Cache;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ProductReviewsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IOutputCacheInvalidationService _outputCacheInvalidationService;

    public ProductReviewsController(
        ISender sender,
        IOutputCacheInvalidationService outputCacheInvalidationService)
    {
        _sender = sender;
        _outputCacheInvalidationService = outputCacheInvalidationService;
    }

    [HttpGet]
    [OutputCache(PolicyName = CachePolicyNames.Reviews)]
    public async Task<ActionResult<PagedResponse<ProductReviewResponse>>> GetAll([FromQuery] ProductReviewFilter filter, CancellationToken ct)
    {
        var reviews = await _sender.Send(new GetProductReviewsQuery(filter), ct);
        return Ok(reviews);
    }

    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = CachePolicyNames.Reviews)]
    public async Task<ActionResult<ProductReviewResponse>> GetById(Guid id, CancellationToken ct)
    {
        var review = await _sender.Send(new GetProductReviewByIdQuery(id), ct);
        return review is null ? NotFound() : Ok(review);
    }

    [HttpGet("by-product/{productId:guid}")]
    [OutputCache(PolicyName = CachePolicyNames.Reviews)]
    public async Task<ActionResult<IEnumerable<ProductReviewResponse>>> GetByProductId(Guid productId, CancellationToken ct)
    {
        var reviews = await _sender.Send(new GetProductReviewsByProductIdQuery(productId), ct);
        return Ok(reviews);
    }

    [HttpPost]
    public async Task<ActionResult<ProductReviewResponse>> Create(CreateProductReviewRequest request, CancellationToken ct)
    {
        var review = await _sender.Send(new CreateProductReviewCommand(request), ct);
        await _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Reviews, ct);
        return CreatedAtAction(nameof(GetById), new { id = review.Id, version = "1.0" }, review);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteProductReviewCommand(id), ct);
        await _outputCacheInvalidationService.EvictAsync(CachePolicyNames.Reviews, ct);
        return NoContent();
    }
}
