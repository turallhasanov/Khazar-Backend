namespace Khazar.Application.DTOs.Auth;

public sealed record LoginRequestDto
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
