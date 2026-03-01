using APITemplate.Application.DTOs;

namespace APITemplate.Application.Interfaces;

public interface ITokenService
{
    TokenResponse GenerateToken(string username);
}
