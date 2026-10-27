using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Khazar.Infrastructure.Persistence;

/// <summary>
/// Creates the SuperAdmin account with a confirmed email address when it does not exist yet. An existing account
/// with that email is left untouched, so a password changed after the first sign-in is never reset on restart.
/// </summary>
public static class SuperAdminSeeder
{
    public static async Task SeedAsync(
        KhazarDbContext context,
        IPasswordHasher passwordHasher,
        SuperAdminSeedOptions options,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsConfigured)
        {
            logger?.LogInformation(
                "'{Section}:Email' or '{Section}:Password' is not configured; SuperAdmin seeding skipped.",
                SuperAdminSeedOptions.SectionName, SuperAdminSeedOptions.SectionName);
            return;
        }

        // Emails are stored lower-cased (Guard.Email), so compare against the normalized form.
        var email = options.Email.Trim().ToLowerInvariant();
        if (await context.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            logger?.LogInformation("SuperAdmin account {Email} already exists; seeding skipped.", email);
            return;
        }

        var admin = new User(options.FirstName, options.LastName, email, passwordHasher.Hash(options.Password));
        admin.MarkEmailConfirmed(DateTime.UtcNow);

        await context.Users.AddAsync(admin, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger?.LogInformation("Seeded SuperAdmin account {Email}.", email);
    }
}
