using APITemplate.Api.Authorization;
using APITemplate.Api.Cache;
using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.TenantInvitation;
using APITemplate.Application.Features.TenantInvitation.DTOs;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/tenant-invitations")]
public sealed class TenantInvitationsController : ControllerBase
{
    private readonly ISender _sender;

    public TenantInvitationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [RequirePermission(Permission.Invitations.Read)]
    [OutputCache(PolicyName = CachePolicyNames.TenantInvitations)]
    public async Task<ActionResult<PagedResponse<TenantInvitationResponse>>> GetAll(
        [FromQuery] TenantInvitationFilter filter,
        CancellationToken ct
    )
    {
        var result = await _sender.Send(new GetTenantInvitationsQuery(filter), ct);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permission.Invitations.Create)]
    public async Task<ActionResult<TenantInvitationResponse>> Create(
        CreateTenantInvitationRequest request,
        CancellationToken ct
    )
    {
        var invitation = await _sender.Send(new CreateTenantInvitationCommand(request), ct);
        return CreatedAtAction(nameof(GetAll), new { version = "1.0" }, invitation);
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken ct
    )
    {
        await _sender.Send(new AcceptTenantInvitationCommand(request.Token), ct);
        return Ok();
    }

    [HttpPatch("{id:guid}/revoke")]
    [RequirePermission(Permission.Invitations.Revoke)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _sender.Send(new RevokeTenantInvitationCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/resend")]
    [RequirePermission(Permission.Invitations.Create)]
    public async Task<IActionResult> Resend(Guid id, CancellationToken ct)
    {
        await _sender.Send(new ResendTenantInvitationCommand(id), ct);
        return Ok();
    }
}
