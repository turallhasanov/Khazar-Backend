using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Khazar.Infrastructure.Persistence.Configurations;

public sealed class NewsletterSubscriptionConfiguration : IEntityTypeConfiguration<NewsletterSubscription>
{
    public const string EmailIndexName = "IX_NewsletterSubscriptions_Email";

    public void Configure(EntityTypeBuilder<NewsletterSubscription> builder)
    {
        builder.ToTable("NewsletterSubscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Email).IsRequired().HasMaxLength(320);

        // Emails are stored lower-case by the entity, so this index also enforces case-insensitive uniqueness.
        builder.HasIndex(s => s.Email).IsUnique().HasDatabaseName(EmailIndexName);
    }
}
