using APITemplate.Application.Features.User.Interfaces;

namespace APITemplate.Infrastructure.Security;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plainTextPassword)
        => BCrypt.Net.BCrypt.EnhancedHashPassword(plainTextPassword);

    public bool Verify(string plainTextPassword, string passwordHash)
        => BCrypt.Net.BCrypt.EnhancedVerify(plainTextPassword, passwordHash);
}
