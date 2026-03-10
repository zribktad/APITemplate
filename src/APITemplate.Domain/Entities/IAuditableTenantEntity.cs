namespace APITemplate.Domain.Entities;

public interface IAuditableTenantEntity : ITenantEntity, IAuditableEntity, ISoftDeletable
{
}
