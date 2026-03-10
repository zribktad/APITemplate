using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetAll(
        [FromQuery] UserFilter filter, CancellationToken ct)
    {
        var result = await _userService.GetPagedAsync(filter, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(AuthConstants.Claims.Subject);

        if (userId is null || !Guid.TryParse(userId, out var id))
            return Unauthorized();

        var user = await _userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.CreateAsync(request, ct);
        var version = HttpContext.GetRequestedApiVersion()?.ToString() ?? "1.0";
        return CreatedAtAction(nameof(GetById), new { id = user.Id, version }, user);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        await _userService.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await _userService.ActivateAsync(id, ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _userService.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/role")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken ct)
    {
        await _userService.ChangeRoleAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _userService.DeleteAsync(id, ct);
        return NoContent();
    }
}
