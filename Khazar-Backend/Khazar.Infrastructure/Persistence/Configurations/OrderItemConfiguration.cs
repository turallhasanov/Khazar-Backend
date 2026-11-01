using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Khazar.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Size).IsRequired().HasMaxLength(16);
        builder.Property(i => i.Color).IsRequired().HasMaxLength(64);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        builder.Ignore(i => i.LineTotal);

        // Order lines are snapshots: ProductId and VariantId are plain columns without foreign keys,
        // so deleting a product never touches order history.
        builder.HasIndex(i => i.ProductId);
        builder.HasIndex(i => i.VariantId);
    }
}
