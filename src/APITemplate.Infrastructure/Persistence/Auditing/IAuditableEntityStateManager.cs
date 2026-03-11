using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence.Auditing;

public interface IAuditableEntityStateManager
{
    void StampAdded(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        bool hasTenant,
        Guid currentTenantId);

    void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor);

    void MarkSoftDeleted(EntityEntry entry, IAuditableTenantEntity entity, DateTime now, Guid actor);
}
