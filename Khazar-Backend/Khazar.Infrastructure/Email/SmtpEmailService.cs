using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Khazar.Infrastructure.Email;

/// <summary>
/// Sends email through SMTP with MailKit. One connection per message: transactional emails are rare enough
/// that pooling connections is not worth the reconnect handling.
/// </summary>
public sealed class SmtpEmailService(SmtpOptions options, StorefrontOptions storefront, ILogger<SmtpEmailService> logger)
    : EmailServiceBase(storefront)
{
    private const int ImplicitTlsPort = 465;

    protected override async Task<bool> DeliverAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var socketOptions = !options.EnableSsl
            ? SecureSocketOptions.None
            : options.Port == ImplicitTlsPort ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        try
        {
            // Inside the try: an address MimeKit cannot parse is a delivery failure too, not an exception for the caller.
            using var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
            message.To.Add(new MailboxAddress(email.ToName, email.ToEmail));
            message.Subject = email.Subject;
            message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();

            using var client = new SmtpClient { Timeout = (int)TimeSpan.FromSeconds(options.TimeoutSeconds).TotalMilliseconds };

            await client.ConnectAsync(options.Host, options.Port, socketOptions, cancellationToken);
            if (!string.IsNullOrEmpty(options.Username))
                await client.AuthenticateAsync(options.Username, options.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation("Email '{Subject}' was sent to {Email}.", email.Subject, email.ToEmail);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // MailKit reports failures with many exception types (socket, TLS, authentication, SMTP command).
            logger.LogError(ex, "Email '{Subject}' to {Email} could not be sent through {Host}:{Port}.",
                email.Subject, email.ToEmail, options.Host, options.Port);
            return false;
        }
    }
}
