using Khazar.Application.DTOs.Auth;

namespace Khazar.Application.Services;

/// <summary>Registration, email verification and token-based sign-in use cases.</summary>
public interface IAuthAppService
{
    /// <summary>Creates an unconfirmed account and emails a 6-digit confirmation code.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">A field is missing or the password is too weak.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The email address is already registered.</exception>
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Confirms the email address and signs the user in.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">The code is wrong, expired or out of attempts.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The email address is already confirmed.</exception>
    Task<AuthResponseDto> VerifyEmailAsync(VerifyEmailRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues and emails a new confirmation code. Unknown or already confirmed addresses are ignored silently,
    /// so the endpoint does not reveal which addresses have pending accounts.
    /// </summary>
    Task ResendVerificationCodeAsync(ResendVerificationRequestDto dto, CancellationToken cancellationToken = default);

    /// <exception cref="Common.Exceptions.UnauthorizedException">
    /// Wrong credentials ("invalid_credentials") or an unconfirmed email address ("email_not_confirmed").
    /// </exception>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Exchanges a refresh token for a new token pair; the presented refresh token stops working.</summary>
    /// <exception cref="Common.Exceptions.UnauthorizedException">The refresh token is unknown, expired or revoked.</exception>
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Revokes the refresh token. Unknown or already revoked tokens are ignored.</summary>
    Task LogoutAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default);

    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
