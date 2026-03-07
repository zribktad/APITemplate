namespace APITemplate.Domain.Entities;

public sealed class ProductDataLink : IAuditableTenantEntity
{
    public Guid ProductId { get; set; }

    public Guid ProductDataId { get; set; }

    public Guid TenantId { get; set; }

    public AuditInfo Audit { get; set; } = new();

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    public Product Product { get; set; } = null!;

    public static ProductDataLink Create(Guid productId, Guid productDataId) =>
        new()
        {
            ProductId = productId,
            ProductDataId = productDataId
        };

    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedBy = null;
    }
}
