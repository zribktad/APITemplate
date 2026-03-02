using APITemplate.Application.Interfaces;

namespace APITemplate.Application.Services;

/// <summary>
/// Demo implementation — validates against credentials stored in configuration.
/// Replace with a real user store (database, identity provider, LDAP) in production.
/// Passwords in production must be stored as hashed values (e.g. PBKDF2, bcrypt).
/// </summary>
public sealed class UserService : IUserService
{
    private readonly IConfiguration _configuration;

    public UserService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<bool> ValidateAsync(string username, string password, CancellationToken ct = default)
    {
        var demoUsername = _configuration["Auth:Username"];
        var demoPassword = _configuration["Auth:Password"];

        var isValid = string.Equals(username, demoUsername, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(password, demoPassword);

        return Task.FromResult(isValid);
    }
}
