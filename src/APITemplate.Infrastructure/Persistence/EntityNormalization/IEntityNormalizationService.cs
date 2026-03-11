using APITemplate.Domain.Entities;

namespace APITemplate.Infrastructure.Persistence.EntityNormalization;

public interface IEntityNormalizationService
{
    void Normalize(IAuditableTenantEntity entity);
}
