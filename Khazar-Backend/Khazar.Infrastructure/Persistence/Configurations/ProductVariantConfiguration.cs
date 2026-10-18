using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Khazar.Infrastructure.Persistence.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Size).IsRequired().HasMaxLength(16);
        builder.Property(v => v.Color).IsRequired().HasMaxLength(64);
        builder.Property(v => v.SKU).IsRequired().HasMaxLength(64);

        // Optimistic concurrency: two checkouts reserving the same stock cannot both succeed.
        builder.Property(v => v.StockQuantity).IsConcurrencyToken();

        builder.Ignore(v => v.IsInStock);

        builder.HasIndex(v => v.SKU).IsUnique();
        // Checkout resolves a variant by color and size, so the pair must be unique per product.
        builder.HasIndex(v => new { v.ProductId, v.Color, v.Size }).IsUnique();
    }
}
