using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

public sealed class ProductDataLinkConfiguration : IEntityTypeConfiguration<ProductDataLink>
{
    public void Configure(EntityTypeBuilder<ProductDataLink> builder)
    {
        builder.HasKey(x => new { x.ProductId, x.ProductDataId });
        builder.ConfigureTenantAuditable();

        builder.HasIndex(x => new { x.TenantId, x.ProductDataId, x.IsDeleted });

        builder.HasOne(x => x.Product)
            .WithMany(p => p.ProductDataLinks)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
