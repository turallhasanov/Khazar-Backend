namespace Khazar.Application.DTOs.Auth;

/// <summary>Tokens returned by login, email verification and refresh. Send the access token as "Authorization: Bearer".</summary>
public sealed record AuthResponseDto
{
    public string TokenType { get; init; } = "Bearer";
    public string AccessToken { get; init; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; init; }

    /// <summary>Single-use: POST /api/auth/refresh-token returns a replacement and invalidates this one.</summary>
    public string RefreshToken { get; init; } = string.Empty;

    public DateTime RefreshTokenExpiresAt { get; init; }
    public UserDto User { get; init; } = new();
}
