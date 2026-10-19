namespace Khazar.Application.DTOs.Auth;

public sealed record RegisterResponseDto
{
    public string Email { get; init; } = string.Empty;

    /// <summary>False when the account was created but the email could not be sent; the client should offer a resend.</summary>
    public bool VerificationEmailSent { get; init; }

    public DateTime CodeExpiresAt { get; init; }
}
