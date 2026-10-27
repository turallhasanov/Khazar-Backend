namespace Khazar.Infrastructure.Email;

/// <summary>A rendered email, ready for delivery.</summary>
/// <param name="LogDetail">
/// What a developer needs from the email when delivery is disabled (the code, the link), written to the log by
/// <see cref="LogEmailService"/> only.
/// </param>
public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string TextBody,
    string HtmlBody,
    string LogDetail);
