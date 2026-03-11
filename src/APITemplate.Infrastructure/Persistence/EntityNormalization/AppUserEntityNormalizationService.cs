using APITemplate.Domain.Entities;

namespace APITemplate.Infrastructure.Persistence.EntityNormalization;

public sealed class AppUserEntityNormalizationService : IEntityNormalizationService
{
    public void Normalize(IAuditableTenantEntity entity)
    {
        if (entity is not AppUser user)
            return;

        user.NormalizedUsername = AppUser.NormalizeUsername(user.Username);
        user.NormalizedEmail = AppUser.NormalizeEmail(user.Email);
    }
}
