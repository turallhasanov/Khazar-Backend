using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Khazar.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public const string EmailIndexName = "IX_Users_Email";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);

        // SHA-256 as upper-case hex.
        builder.Property(u => u.ConfirmationToken).HasMaxLength(64).IsUnicode(false);

        builder.Ignore(u => u.FullName);

        // Emails are stored lower-case by the User entity, so this index also enforces case-insensitive uniqueness.
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(EmailIndexName);
    }
}
