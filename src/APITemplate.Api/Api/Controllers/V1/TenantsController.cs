using APITemplate.Api.Authorization;
using APITemplate.Application.Common.Security;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class TenantsController : ControllerBase
{
    private readonly ISender _sender;

    public TenantsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [RequirePermission(Permission.Tenants.Read)]
    public async Task<ActionResult<PagedResponse<TenantResponse>>> GetAll(
        [FromQuery] TenantFilter filter,
        CancellationToken ct
    )
    {
        var tenants = await _sender.Send(new GetTenantsQuery(filter), ct);
        return Ok(tenants);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Tenants.Read)]
    public async Task<ActionResult<TenantResponse>> GetById(Guid id, CancellationToken ct)
    {
        var tenant = await _sender.Send(new GetTenantByIdQuery(id), ct);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    [RequirePermission(Permission.Tenants.Create)]
    public async Task<ActionResult<TenantResponse>> Create(
        CreateTenantRequest request,
        CancellationToken ct
    )
    {
        var tenant = await _sender.Send(new CreateTenantCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id, version = "1.0" }, tenant);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Tenants.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteTenantCommand(id), ct);
        return NoContent();
    }
}
