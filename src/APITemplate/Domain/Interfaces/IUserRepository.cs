using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IUserRepository : IRepository<AppUser>
{
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string normalizedUsername, CancellationToken ct = default);
}
