namespace Khazar.Application.DTOs.Auth;

public sealed record RefreshTokenRequestDto
{
    public string RefreshToken { get; init; } = string.Empty;
}
