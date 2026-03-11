using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence.Auditing;

public sealed class AuditableEntityStateManager : IAuditableEntityStateManager
{
    public void StampAdded(
        EntityEntry entry,
        IAuditableTenantEntity entity,
        DateTime now,
        Guid actor,
        bool hasTenant,
        Guid currentTenantId)
    {
        if (entity is Tenant tenant && tenant.TenantId == Guid.Empty)
            tenant.TenantId = tenant.Id;

        if (entity.TenantId == Guid.Empty && hasTenant)
            entity.TenantId = currentTenantId;

        entity.Audit.CreatedAtUtc = now;
        entity.Audit.CreatedBy = actor;
        StampModified(entity, now, actor);
        ResetSoftDelete(entity);
        entry.State = EntityState.Added;
    }

    public void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor)
    {
        entity.Audit.UpdatedAtUtc = now;
        entity.Audit.UpdatedBy = actor;
    }

    public void MarkSoftDeleted(EntityEntry entry, IAuditableTenantEntity entity, DateTime now, Guid actor)
    {
        entry.State = EntityState.Modified;
        entity.IsDeleted = true;
        entity.DeletedAtUtc = now;
        entity.DeletedBy = actor;
        StampModified(entity, now, actor);
        EnsureAuditOwnedEntryState(entry, now, actor);
    }

    private static void ResetSoftDelete(IAuditableTenantEntity entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAtUtc = null;
        entity.DeletedBy = null;
    }

    private static void EnsureAuditOwnedEntryState(EntityEntry ownerEntry, DateTime now, Guid actor)
    {
        var auditEntry = ownerEntry.Reference(nameof(IAuditableTenantEntity.Audit)).TargetEntry;
        if (auditEntry is null)
            return;

        if (auditEntry.State is EntityState.Deleted or EntityState.Detached or EntityState.Unchanged)
            auditEntry.State = EntityState.Modified;

        auditEntry.Property(nameof(AuditInfo.UpdatedAtUtc)).CurrentValue = now;
        auditEntry.Property(nameof(AuditInfo.UpdatedBy)).CurrentValue = actor;
    }
}
