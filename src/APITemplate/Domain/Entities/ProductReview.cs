namespace APITemplate.Domain.Entities;

public sealed class ProductReview : IAuditableTenantEntity
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string? Comment { get; set; }
    public int Rating { get; set; }
    public Product Product { get; set; } = null!;
    public AppUser User { get; set; } = null!;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
