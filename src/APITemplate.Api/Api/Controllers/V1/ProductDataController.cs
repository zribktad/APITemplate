using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/product-data")]
public sealed class ProductDataController : ControllerBase
{
    private readonly ISender _sender;

    public ProductDataController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductDataResponse>>> GetAll([FromQuery] string? type, CancellationToken ct)
    {
        var items = await _sender.Send(new GetProductDataQuery(type), ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDataResponse>> GetById(Guid id, CancellationToken ct)
    {
        var item = await _sender.Send(new GetProductDataByIdQuery(id), ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("image")]
    public async Task<ActionResult<ProductDataResponse>> CreateImage(
        CreateImageProductDataRequest request, CancellationToken ct)
    {
        var created = await _sender.Send(new CreateImageProductDataCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1.0" }, created);
    }

    [HttpPost("video")]
    public async Task<ActionResult<ProductDataResponse>> CreateVideo(
        CreateVideoProductDataRequest request, CancellationToken ct)
    {
        var created = await _sender.Send(new CreateVideoProductDataCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1.0" }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteProductDataCommand(id), ct);
        return NoContent();
    }
}
