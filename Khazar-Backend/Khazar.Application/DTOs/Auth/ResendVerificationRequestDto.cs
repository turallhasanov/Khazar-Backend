namespace Khazar.Application.DTOs.Auth;

public sealed record ResendVerificationRequestDto
{
    public string Email { get; init; } = string.Empty;
}
