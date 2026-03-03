using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/product-data")]
[Authorize]
public sealed class ProductDataController : ControllerBase
{
    private readonly IProductDataService _service;

    public ProductDataController(IProductDataService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductDataResponse>>> GetAll([FromQuery] string? type, CancellationToken ct)
    {
        var items = await _service.GetAllAsync(type, ct);
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDataResponse>> GetById(string id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("image")]
    public async Task<ActionResult<ProductDataResponse>> CreateImage(
        CreateImageProductDataRequest request, CancellationToken ct)
    {
        var created = await _service.CreateImageAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1.0" }, created);
    }

    [HttpPost("video")]
    public async Task<ActionResult<ProductDataResponse>> CreateVideo(
        CreateVideoProductDataRequest request, CancellationToken ct)
    {
        var created = await _service.CreateVideoAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1.0" }, created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
