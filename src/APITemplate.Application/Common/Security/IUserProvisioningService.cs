using APITemplate.Domain.Entities;

namespace APITemplate.Application.Common.Security;

public interface IUserProvisioningService
{
    Task<AppUser?> ProvisionIfNeededAsync(
        string keycloakUserId,
        string email,
        string username,
        CancellationToken ct = default);
}
