namespace Khazar.Application.DTOs.Auth;

public sealed record VerifyEmailRequestDto
{
    public string Email { get; init; } = string.Empty;

    /// <summary>The 6-digit code from the welcome email.</summary>
    public string Code { get; init; } = string.Empty;
}
