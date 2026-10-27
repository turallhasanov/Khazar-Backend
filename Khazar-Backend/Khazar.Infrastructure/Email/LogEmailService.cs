using Microsoft.Extensions.Logging;

namespace Khazar.Infrastructure.Email;

/// <summary>
/// Development stand-in for <see cref="SmtpEmailService"/> (Smtp:DeliveryMethod = Log): instead of sending, the
/// subject, recipient and the part a developer needs (verification code, reset link, discount code) are written to
/// the log, so every email flow can be tested without an SMTP server. Never enable it in production.
/// </summary>
public sealed class LogEmailService(StorefrontOptions storefront, ILogger<LogEmailService> logger)
    : EmailServiceBase(storefront)
{
    protected override Task<bool> DeliverAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "SMTP delivery is disabled; email '{Subject}' to {Email} was not sent. {Detail}",
            message.Subject, message.ToEmail, message.LogDetail);

        return Task.FromResult(true);
    }
}
