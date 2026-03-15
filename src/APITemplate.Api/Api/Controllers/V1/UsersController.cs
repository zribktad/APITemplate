using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using APITemplate.Api.Authorization;
using APITemplate.Api.Cache;
using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.User.DTOs;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [RequirePermission(Permission.Users.Read)]
    [OutputCache(PolicyName = CachePolicyNames.Users)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetAll(
        [FromQuery] UserFilter filter,
        CancellationToken ct
    )
    {
        var result = await _sender.Send(new GetUsersQuery(filter), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Users.Read)]
    [OutputCache(PolicyName = CachePolicyNames.Users)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _sender.Send(new GetUserByIdQuery(id), ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("me")]
    [OutputCache(PolicyName = CachePolicyNames.NoCache)]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken ct)
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(AuthConstants.Claims.Subject);

        if (userId is null || !Guid.TryParse(userId, out var id))
            return Unauthorized();

        var user = await _sender.Send(new GetUserByIdQuery(id), ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [RequirePermission(Permission.Users.Create)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken ct
    )
    {
        var user = await _sender.Send(new CreateUserCommand(request), ct);
        var version = HttpContext.GetRequestedApiVersion()?.ToString() ?? "1.0";
        return CreatedAtAction(nameof(GetById), new { id = user.Id, version }, user);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct
    )
    {
        await _sender.Send(new UpdateUserCommand(id, request), ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await _sender.Send(new ActivateUserCommand(id), ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateUserCommand(id), ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/role")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        ChangeUserRoleRequest request,
        CancellationToken ct
    )
    {
        await _sender.Send(new ChangeUserRoleCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Users.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteUserCommand(id), ct);
        return NoContent();
    }

    [HttpPost("password-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordReset(
        RequestPasswordResetRequest request,
        CancellationToken ct
    )
    {
        await _sender.Send(new KeycloakPasswordResetCommand(request), ct);
        return Ok();
    }
}
