using Khazar.Application.Common.Pricing;
using Khazar.Application.DTOs.Orders;

namespace Khazar.Application.Common.Interfaces;

/// <summary>
/// Outgoing email. Implementations never throw on delivery failures (unreachable server, rejected credentials):
/// failures are logged and reported through the return value. Caller cancellation is still propagated.
/// Every method returns true when the message was handed off to the mail server.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends the welcome email with the email confirmation code.</summary>
    Task<bool> SendVerificationCodeAsync(
        string toEmail, string recipientName, string code, TimeSpan validFor, CancellationToken cancellationToken = default);

    /// <summary>Welcomes a new newsletter subscriber; includes the welcome discount code when one is configured.</summary>
    Task<bool> SendNewsletterWelcomeAsync(string toEmail, CancellationToken cancellationToken = default);

    /// <summary>Sends the receipt of a placed order (items, shipping fee, grand total) to the customer's email address.</summary>
    Task<bool> SendOrderConfirmationAsync(
        OrderResponseDto order, ShippingQuote shipping, CancellationToken cancellationToken = default);

    /// <summary>Sends the link to the storefront's "set a new password" page, carrying the one-time reset token.</summary>
    Task<bool> SendPasswordResetAsync(
        string toEmail, string recipientName, string resetToken, TimeSpan validFor, CancellationToken cancellationToken = default);
}
