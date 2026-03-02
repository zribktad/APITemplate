using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace APITemplate.Application.Services;

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TokenResponse GenerateToken(string username)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));

        var expires = DateTime.UtcNow.AddMinutes(
            double.Parse(jwtSettings["ExpirationMinutes"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires);
    }
}
