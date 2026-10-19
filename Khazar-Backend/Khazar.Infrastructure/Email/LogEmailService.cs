using Khazar.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Khazar.Infrastructure.Email;

/// <summary>
/// Development stand-in for <see cref="SmtpEmailService"/> (Smtp:DeliveryMethod = Log): the verification code is
/// written to the log, so registration can be tested without an SMTP server. Never enable it in production.
/// </summary>
public sealed class LogEmailService(ILogger<LogEmailService> logger) : IEmailService
{
    public Task<bool> SendVerificationCodeAsync(
        string toEmail, string recipientName, string code, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "SMTP delivery is disabled; email '{Subject}' to {Email} was not sent. Verification code: {Code} (valid for {Minutes} minutes).",
            VerificationEmail.Subject, toEmail, code, (int)validFor.TotalMinutes);

        return Task.FromResult(true);
    }
}
