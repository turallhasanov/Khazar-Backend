namespace Khazar.Application.DTOs.Auth;

public sealed record RegisterRequestDto
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;

    /// <summary>At least 8 characters with at least one letter and one digit.</summary>
    public string Password { get; init; } = string.Empty;
}
