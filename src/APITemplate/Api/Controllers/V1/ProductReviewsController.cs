using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class ProductReviewsController : ControllerBase
{
    private readonly IProductReviewService _reviewService;

    public ProductReviewsController(IProductReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductReviewResponse>>> GetAll([FromQuery] ProductReviewFilter filter, CancellationToken ct)
    {
        var reviews = await _reviewService.GetAllAsync(filter, ct);
        return Ok(reviews);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductReviewResponse>> GetById(Guid id, CancellationToken ct)
    {
        var review = await _reviewService.GetByIdAsync(id, ct);
        return review is null ? NotFound() : Ok(review);
    }

    [HttpGet("by-product/{productId:guid}")]
    public async Task<ActionResult<IEnumerable<ProductReviewResponse>>> GetByProductId(Guid productId, CancellationToken ct)
    {
        var reviews = await _reviewService.GetByProductIdAsync(productId, ct);
        return Ok(reviews);
    }

    [HttpPost]
    public async Task<ActionResult<ProductReviewResponse>> Create(CreateProductReviewRequest request, CancellationToken ct)
    {
        var review = await _reviewService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = review.Id, version = "1.0" }, review);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _reviewService.DeleteAsync(id, ct);
        return NoContent();
    }
}
