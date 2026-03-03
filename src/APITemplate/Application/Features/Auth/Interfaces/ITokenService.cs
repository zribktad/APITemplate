
namespace APITemplate.Application.Features.Auth.Interfaces;
public interface ITokenService
{
    TokenResponse GenerateToken(string username);
}
