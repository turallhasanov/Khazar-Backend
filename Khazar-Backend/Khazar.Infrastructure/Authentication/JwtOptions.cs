using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Khazar.Infrastructure.Authentication;

/// <summary>Settings bound from the "Jwt" configuration section; shared by token issuing and validation.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 needs a key of at least 256 bits.
    private const int MinSecretBytes = 32;

    /// <summary>HMAC signing key. Keep it out of source control in production (environment variable or user secrets).</summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Binds and validates the section; misconfiguration fails at startup instead of at the first login.</summary>
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (Encoding.UTF8.GetByteCount(options.Secret) < MinSecretBytes)
            throw new InvalidOperationException(
                $"Configuration value '{SectionName}:{nameof(Secret)}' must be at least {MinSecretBytes} bytes long.");

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException(
                $"Configuration values '{SectionName}:{nameof(Issuer)}' and '{SectionName}:{nameof(Audience)}' are required.");

        if (options.AccessTokenMinutes <= 0 || options.RefreshTokenDays <= 0)
            throw new InvalidOperationException(
                $"Configuration values '{SectionName}:{nameof(AccessTokenMinutes)}' and '{SectionName}:{nameof(RefreshTokenDays)}' must be positive.");

        return options;
    }

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Secret));
}
