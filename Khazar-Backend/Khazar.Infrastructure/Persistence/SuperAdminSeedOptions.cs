using Microsoft.Extensions.Configuration;

namespace Khazar.Infrastructure.Persistence;

/// <summary>
/// Settings bound from the "SuperAdmin" configuration section: the platform admin account created on startup.
/// The email is also treated as an admin address by the "PlatformAdmin" policy, next to "Admin:Emails".
/// </summary>
public sealed class SuperAdminSeedOptions
{
    public const string SectionName = "SuperAdmin";

    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = "Super";
    public string LastName { get; set; } = "Admin";

    /// <summary>
    /// Temporary password for the first sign-in. Empty disables seeding. Keep it out of source control outside
    /// Development (environment variable or user secrets) and change it after the first sign-in.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

    public static SuperAdminSeedOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetSection(SectionName).Get<SuperAdminSeedOptions>() ?? new SuperAdminSeedOptions();
    }
}
