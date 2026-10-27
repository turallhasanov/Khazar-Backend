using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Khazar.Infrastructure.Persistence.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public const string SkuIndexName = "IX_ProductVariants_SKU";
    public const string ColorSizeIndexName = "IX_ProductVariants_ProductId_Color_Size";

    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Size).IsRequired().HasMaxLength(ProductVariant.MaxSizeLength);
        builder.Property(v => v.Color).IsRequired().HasMaxLength(ProductVariant.MaxColorLength);
        builder.Property(v => v.SKU).IsRequired().HasMaxLength(ProductVariant.MaxSkuLength);

        // Optimistic concurrency: two checkouts reserving the same stock cannot both succeed.
        builder.Property(v => v.StockQuantity).IsConcurrencyToken();

        builder.Ignore(v => v.IsInStock);

        builder.HasIndex(v => v.SKU).IsUnique().HasDatabaseName(SkuIndexName);
        // Checkout resolves a variant by color and size, so the pair must be unique per product.
        builder.HasIndex(v => new { v.ProductId, v.Color, v.Size }).IsUnique().HasDatabaseName(ColorSizeIndexName);
    }
}
