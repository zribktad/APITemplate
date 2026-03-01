using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;
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

    public AuthController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login(LoginRequest request)
    {
        // Simplified demo: in production, validate against a user store
        if (request.Username != "admin" || request.Password != "admin")
            return Unauthorized();

        var token = _tokenService.GenerateToken(request.Username);
        return Ok(token);
    }
}
