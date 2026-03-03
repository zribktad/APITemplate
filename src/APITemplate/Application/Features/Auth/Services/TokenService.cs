using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace APITemplate.Application.Features.Auth.Services;
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _jwt;

    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwt = jwtOptions.Value;
    }

    public TokenResponse GenerateToken(string username)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwt.Secret));

        var expires = DateTime.UtcNow.AddMinutes(
            _jwt.ExpirationMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires);
    }
}
