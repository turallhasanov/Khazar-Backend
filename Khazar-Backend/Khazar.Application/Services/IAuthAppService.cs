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

    /// <summary>
    /// Emails a single-use password reset link (valid for 30 minutes) to a confirmed account. Unknown and unconfirmed
    /// addresses are ignored silently, and the email is sent in the background, so the endpoint reveals nothing.
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password with the token from the reset link and signs out every session of the account.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">
    /// The new password is too weak ("weak_password") or the link is invalid, expired or used ("invalid_reset_token").
    /// </exception>
    Task ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default);

    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
