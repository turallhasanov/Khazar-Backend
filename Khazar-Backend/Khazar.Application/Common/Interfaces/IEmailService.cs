namespace Khazar.Application.Common.Interfaces;

/// <summary>
/// Outgoing email. Implementations never throw on delivery failures (unreachable server, rejected credentials):
/// failures are logged and reported through the return value. Caller cancellation is still propagated.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends the welcome email with the email confirmation code. Returns true when the message was handed off.</summary>
    Task<bool> SendVerificationCodeAsync(
        string toEmail, string recipientName, string code, TimeSpan validFor, CancellationToken cancellationToken = default);
}
