using APITemplate.Domain.Enums;

namespace APITemplate.Domain.Entities;

public sealed class TenantInvitation : IAuditableTenantEntity
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Tenant Tenant { get; set; } = null!;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
