using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IUserService _userService;

    public AuthController(ITokenService tokenService, IUserService userService)
    {
        _tokenService = tokenService;
        _userService = userService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var isValid = await _userService.ValidateAsync(request.Username, request.Password, ct);
        if (!isValid)
            return Unauthorized(new LoginErrorResponse("Invalid username or password."));

        var token = _tokenService.GenerateToken(request.Username);
        return Ok(token);
    }
}
