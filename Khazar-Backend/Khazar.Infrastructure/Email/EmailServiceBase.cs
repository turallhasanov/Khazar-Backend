using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Pricing;
using Khazar.Application.DTOs.Orders;

namespace Khazar.Infrastructure.Email;

/// <summary>
/// Renders every email type the same way for all delivery methods; subclasses only decide how a rendered
/// <see cref="EmailMessage"/> is delivered (SMTP, or the log in development).
/// </summary>
public abstract class EmailServiceBase(StorefrontOptions storefront) : IEmailService
{
    public Task<bool> SendVerificationCodeAsync(
        string toEmail, string recipientName, string code, TimeSpan validFor, CancellationToken cancellationToken = default) =>
        DeliverAsync(new EmailMessage(
            toEmail,
            recipientName,
            VerificationEmail.Subject,
            VerificationEmail.TextBody(recipientName, code, validFor),
            VerificationEmail.HtmlBody(recipientName, code, validFor),
            $"Verification code: {code} (valid for {(int)validFor.TotalMinutes} minutes)"), cancellationToken);

    public Task<bool> SendNewsletterWelcomeAsync(string toEmail, CancellationToken cancellationToken = default) =>
        DeliverAsync(NewsletterWelcomeEmail.Create(toEmail, storefront), cancellationToken);

    public Task<bool> SendOrderConfirmationAsync(
        OrderResponseDto order, ShippingQuote shipping, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(shipping);
        return DeliverAsync(OrderConfirmationEmail.Create(order, shipping, storefront), cancellationToken);
    }

    public Task<bool> SendPasswordResetAsync(
        string toEmail, string recipientName, string resetToken, TimeSpan validFor, CancellationToken cancellationToken = default) =>
        DeliverAsync(PasswordResetEmail.Create(toEmail, recipientName, resetToken, validFor, storefront), cancellationToken);

    /// <summary>Delivers the message. Must not throw on delivery failures (see <see cref="IEmailService"/>).</summary>
    protected abstract Task<bool> DeliverAsync(EmailMessage message, CancellationToken cancellationToken);
}
