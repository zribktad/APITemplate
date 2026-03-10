namespace APITemplate.Domain.Entities;

public sealed class AuditInfo
{
    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedBy { get; set; } = AuditDefaults.SystemActorId;
    public DateTime UpdatedAtUtc { get; set; }
    public Guid UpdatedBy { get; set; } = AuditDefaults.SystemActorId;
}
