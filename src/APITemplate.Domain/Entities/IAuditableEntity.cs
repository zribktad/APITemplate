namespace APITemplate.Domain.Entities;

public interface IAuditableEntity
{
    AuditInfo Audit { get; set; }
}
