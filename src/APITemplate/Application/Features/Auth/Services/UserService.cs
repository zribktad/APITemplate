using Microsoft.Extensions.Options;

namespace APITemplate.Application.Features.Auth.Services;
/// <summary>
/// Demo implementation — validates against credentials stored in configuration.
/// Replace with a real user store (database, identity provider, LDAP) in production.
/// Passwords in production must be stored as hashed values (e.g. PBKDF2, bcrypt).
/// </summary>
public sealed class UserService : IUserService
{
    private readonly AuthOptions _auth;

    public UserService(IOptions<AuthOptions> authOptions)
    {
        _auth = authOptions.Value;
    }

    public Task<bool> ValidateAsync(string username, string password, CancellationToken ct = default)
    {
        var isValid = string.Equals(username, _auth.Username, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(password, _auth.Password);

        return Task.FromResult(isValid);
    }
}
