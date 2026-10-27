namespace Khazar.Application.DTOs.Auth;

/// <summary>Body of POST /api/auth/reset-password; email and token come from the link in the reset email.</summary>
public sealed record ResetPasswordRequestDto
{
    public string Email { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
