using Microsoft.Extensions.Configuration;

namespace Khazar.Infrastructure.Email;

public enum EmailDeliveryMethod
{
    /// <summary>Send through the configured SMTP server.</summary>
    Smtp,

    /// <summary>Development only: write the email (including the verification code) to the log instead of sending it.</summary>
    Log,
}

/// <summary>Settings bound from the "Smtp" configuration section.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public EmailDeliveryMethod DeliveryMethod { get; set; } = EmailDeliveryMethod.Smtp;

    public string Host { get; set; } = string.Empty;

    /// <summary>587 (STARTTLS) or 465 (implicit TLS).</summary>
    public int Port { get; set; } = 587;

    /// <summary>Optional: leave empty for servers that accept unauthenticated mail (e.g. a local relay).</summary>
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>TLS on port 465 is implicit; on any other port the connection is upgraded with STARTTLS.</summary>
    public bool EnableSsl { get; set; } = true;

    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Khazar";
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Binds and validates the section; SMTP settings are required only when DeliveryMethod is Smtp.</summary>
    public static SmtpOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(SectionName).Get<SmtpOptions>() ?? new SmtpOptions();
        if (options.DeliveryMethod != EmailDeliveryMethod.Smtp)
            return options;

        if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.FromEmail))
            throw new InvalidOperationException(
                $"Configuration values '{SectionName}:{nameof(Host)}' and '{SectionName}:{nameof(FromEmail)}' are required " +
                $"when '{SectionName}:{nameof(DeliveryMethod)}' is '{EmailDeliveryMethod.Smtp}'.");

        if (options.Port is <= 0 or > 65535 || options.TimeoutSeconds <= 0)
            throw new InvalidOperationException(
                $"Configuration values '{SectionName}:{nameof(Port)}' and '{SectionName}:{nameof(TimeoutSeconds)}' are out of range.");

        return options;
    }
}
