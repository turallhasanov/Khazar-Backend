using Khazar.Infrastructure.Persistence;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Khazar.WebAPI.Authentication;

/// <summary>
/// "PlatformAdmin" policy for the /api/admin endpoints. Accounts have no roles yet, so admins are the signed-in users
/// whose email address is listed in "Admin:Emails". Only confirmed addresses can sign in, so the list cannot be
/// claimed by registering one of its addresses.
/// </summary>
public static class AdminAuthorizationExtensions
{
    public const string PlatformAdminPolicy = "PlatformAdmin";
    public const string AdminEmailsKey = "Admin:Emails";

    public static IServiceCollection AddPlatformAdminAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        // The seeded SuperAdmin ("SuperAdmin:Email") is an admin without having to be repeated in "Admin:Emails".
        var adminEmails = (configuration.GetSection(AdminEmailsKey).Get<string[]>() ?? [])
            .Append(SuperAdminSeedOptions.FromConfiguration(configuration).Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        services.AddAuthorizationBuilder()
            .AddPolicy(PlatformAdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    context.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value is { } email
                    && adminEmails.Contains(email)));

        return services;
    }
}
