using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Khazar.Infrastructure.Persistence.Configurations;

public sealed class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public const string ProductUserIndexName = "IX_ProductReviews_ProductId_UserId";

    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("ProductReviews", table =>
            table.HasCheckConstraint(
                "CK_ProductReviews_Rating",
                $"[Rating] BETWEEN {ProductReview.MinRating} AND {ProductReview.MaxRating}"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.UserName).IsRequired().HasMaxLength(ProductReview.MaxUserNameLength);
        builder.Property(r => r.Comment).IsRequired().HasMaxLength(ProductReview.MaxCommentLength);

        // Stored as text so the column stays readable and enum reordering cannot corrupt data.
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);

        // One review per user and product; also backs the duplicate check of ProductReviewRepository.AddAsync.
        builder.HasIndex(r => new { r.ProductId, r.UserId }).IsUnique().HasDatabaseName(ProductUserIndexName);

        // The product page reads approved reviews; the moderation queue filters by status.
        builder.HasIndex(r => new { r.ProductId, r.Status });
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.UserId);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
