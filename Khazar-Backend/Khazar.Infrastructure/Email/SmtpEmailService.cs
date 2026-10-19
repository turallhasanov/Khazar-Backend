using Khazar.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Khazar.Infrastructure.Email;

/// <summary>
/// Sends email through SMTP with MailKit. One connection per message: verification emails are rare enough
/// that pooling connections is not worth the reconnect handling.
/// </summary>
public sealed class SmtpEmailService(SmtpOptions options, ILogger<SmtpEmailService> logger) : IEmailService
{
    private const int ImplicitTlsPort = 465;

    public async Task<bool> SendVerificationCodeAsync(
        string toEmail, string recipientName, string code, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
        message.To.Add(new MailboxAddress(recipientName, toEmail));
        message.Subject = VerificationEmail.Subject;
        message.Body = new BodyBuilder
        {
            TextBody = VerificationEmail.TextBody(recipientName, code, validFor),
            HtmlBody = VerificationEmail.HtmlBody(recipientName, code, validFor),
        }.ToMessageBody();

        return await SendAsync(message, toEmail, cancellationToken);
    }

    private async Task<bool> SendAsync(MimeMessage message, string toEmail, CancellationToken cancellationToken)
    {
        var socketOptions = !options.EnableSsl
            ? SecureSocketOptions.None
            : options.Port == ImplicitTlsPort ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        try
        {
            using var client = new SmtpClient { Timeout = (int)TimeSpan.FromSeconds(options.TimeoutSeconds).TotalMilliseconds };

            await client.ConnectAsync(options.Host, options.Port, socketOptions, cancellationToken);
            if (!string.IsNullOrEmpty(options.Username))
                await client.AuthenticateAsync(options.Username, options.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation("Email '{Subject}' was sent to {Email}.", message.Subject, toEmail);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // MailKit reports failures with many exception types (socket, TLS, authentication, SMTP command).
            logger.LogError(ex, "Email '{Subject}' to {Email} could not be sent through {Host}:{Port}.",
                message.Subject, toEmail, options.Host, options.Port);
            return false;
        }
    }
}
