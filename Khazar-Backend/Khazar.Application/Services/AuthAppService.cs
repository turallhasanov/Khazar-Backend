using System.Security.Cryptography;
using System.Text;
using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Auth;
using Khazar.Domain.Entities;
using Khazar.Domain.Enums;

namespace Khazar.Application.Services;

public sealed class AuthAppService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IEmailService emailService) : IAuthAppService
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 320;
    private const int VerificationCodeLength = 6;

    private static readonly TimeSpan VerificationCodeLifetime = TimeSpan.FromMinutes(15);

    // Several tabs share one refresh token and may race to rotate it; a token presented again within this window
    // is a lost race, later it is treated as stolen.
    private static readonly TimeSpan RefreshTokenReuseGracePeriod = TimeSpan.FromSeconds(30);

    // Verified when the email is unknown, so a login answers in the same time whether or not the account exists.
    private static string? _dummyPasswordHash;

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        RequireLength(dto.FirstName, MaxNameLength, "First name");
        RequireLength(dto.LastName, MaxNameLength, "Last name");
        var email = NormalizeEmail(dto.Email);
        ValidatePassword(dto.Password);

        if (await userRepository.ExistsByEmailAsync(email, cancellationToken))
            throw new ConflictException("An account with this email address already exists.", "email_already_registered");

        // Name and email shape rules are enforced by the User constructor.
        var user = new User(dto.FirstName, dto.LastName, email, passwordHasher.Hash(dto.Password));
        var code = IssueVerificationCode(user);

        // A concurrent registration of the same address is reported as a ConflictException by the repository.
        await userRepository.AddAsync(user, cancellationToken);

        // The account exists even when the email fails; the client offers a resend in that case.
        var sent = await emailService.SendVerificationCodeAsync(
            user.Email, user.FirstName, code, VerificationCodeLifetime, cancellationToken);

        return new RegisterResponseDto
        {
            Email = user.Email,
            VerificationEmailSent = sent,
            CodeExpiresAt = user.ConfirmationTokenExpiresAt!.Value,
        };
    }

    public async Task<AuthResponseDto> VerifyEmailAsync(VerifyEmailRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var email = NormalizeEmail(dto.Email);
        var code = dto.Code.Trim();
        if (code.Length != VerificationCodeLength || !code.All(char.IsAsciiDigit))
            throw new ValidationException($"The verification code must be {VerificationCodeLength} digits.", "invalid_verification_code");

        // An unknown address gets the same answer as a wrong code.
        var user = await userRepository.GetByEmailAsync(email, cancellationToken)
            ?? throw new ValidationException("The verification code is incorrect.", "invalid_verification_code");

        var result = user.ConfirmEmail(HashVerificationCode(user.Id, code), DateTime.UtcNow);

        // Failed attempts are counted on the user, so it is saved whatever the result.
        await userRepository.UpdateAsync(user, cancellationToken);

        return result switch
        {
            EmailConfirmationResult.Confirmed => await IssueTokensAsync(user, cancellationToken),
            EmailConfirmationResult.AlreadyConfirmed =>
                throw new ConflictException("This email address is already confirmed. Please log in.", "email_already_confirmed"),
            EmailConfirmationResult.Expired =>
                throw new ValidationException("The verification code has expired. Request a new code.", "verification_code_expired"),
            EmailConfirmationResult.TooManyAttempts =>
                throw new ValidationException("Too many incorrect attempts. Request a new code.", "too_many_attempts"),
            _ => throw new ValidationException("The verification code is incorrect.", "invalid_verification_code"),
        };
    }

    public async Task ResendVerificationCodeAsync(ResendVerificationRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await userRepository.GetByEmailAsync(NormalizeEmail(dto.Email), cancellationToken);
        if (user is null || user.IsEmailConfirmed)
            return;

        var code = IssueVerificationCode(user);
        await userRepository.UpdateAsync(user, cancellationToken);
        await emailService.SendVerificationCodeAsync(user.Email, user.FirstName, code, VerificationCodeLifetime, cancellationToken);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var email = NormalizeEmail(dto.Email);
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            passwordHasher.Verify(dto.Password, _dummyPasswordHash ??= passwordHasher.Hash(Guid.NewGuid().ToString()));
            throw InvalidCredentials();
        }

        if (!passwordHasher.Verify(dto.Password, user.PasswordHash))
            throw InvalidCredentials();

        // Checked after the password, so the confirmation state is not revealed to someone without it.
        if (!user.IsEmailConfirmed)
            throw new UnauthorizedException("The email address is not confirmed yet.", "email_not_confirmed");

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            throw InvalidRefreshToken();

        var now = DateTime.UtcNow;
        var stored = await refreshTokenRepository.GetByHashAsync(
            jwtTokenGenerator.HashRefreshToken(dto.RefreshToken.Trim()), cancellationToken)
            ?? throw InvalidRefreshToken();

        if (stored.IsRevoked)
        {
            // A rotated token presented again long after rotation was probably copied: end every session of the user.
            if (stored.ReplacedByTokenHash is not null && now - stored.RevokedAt > RefreshTokenReuseGracePeriod)
                await refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, now, cancellationToken);

            throw InvalidRefreshToken();
        }

        if (!stored.IsActive(now))
            throw InvalidRefreshToken();

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsEmailConfirmed)
            throw InvalidRefreshToken();

        var accessToken = jwtTokenGenerator.GenerateAccessToken(user);
        var refreshToken = jwtTokenGenerator.GenerateRefreshToken();

        // The revocation of the old token is saved together with the new token (same unit of work).
        stored.Revoke(now, refreshToken.TokenHash);
        await refreshTokenRepository.AddAsync(
            new RefreshToken(user.Id, refreshToken.TokenHash, refreshToken.ExpiresAt), cancellationToken);

        return ToAuthResponse(user, accessToken, refreshToken);
    }

    public async Task LogoutAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            return;

        var stored = await refreshTokenRepository.GetByHashAsync(
            jwtTokenGenerator.HashRefreshToken(dto.RefreshToken.Trim()), cancellationToken);
        if (stored is null || stored.IsRevoked)
            return;

        stored.Revoke(DateTime.UtcNow);
        await refreshTokenRepository.UpdateAsync(stored, cancellationToken);
    }

    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : ToUserDto(user);
    }

    private async Task<AuthResponseDto> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = jwtTokenGenerator.GenerateAccessToken(user);
        var refreshToken = jwtTokenGenerator.GenerateRefreshToken();

        await refreshTokenRepository.AddAsync(
            new RefreshToken(user.Id, refreshToken.TokenHash, refreshToken.ExpiresAt), cancellationToken);

        return ToAuthResponse(user, accessToken, refreshToken);
    }

    /// <summary>Generates a new code, stores its hash on the user and returns the plain code for the email.</summary>
    private static string IssueVerificationCode(User user)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.SetConfirmationToken(HashVerificationCode(user.Id, code), DateTime.UtcNow + VerificationCodeLifetime);
        return code;
    }

    // Salted with the user id, so equal codes of different users have different hashes.
    private static string HashVerificationCode(Guid userId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{code}")));

    private static string NormalizeEmail(string? email)
    {
        var value = RequireLength(email, MaxEmailLength, "Email");
        return value.ToLowerInvariant();
    }

    private static string RequireLength(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"{field} is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ValidationException($"{field} must be at most {maxLength} characters.");

        return trimmed;
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password)
            || password.Length is < MinPasswordLength or > MaxPasswordLength
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            throw new ValidationException(
                $"The password must be {MinPasswordLength}-{MaxPasswordLength} characters long and contain at least one letter and one digit.",
                "weak_password");
        }
    }

    private static UnauthorizedException InvalidCredentials() =>
        new("The email address or password is incorrect.", "invalid_credentials");

    private static UnauthorizedException InvalidRefreshToken() =>
        new("The refresh token is invalid or has expired. Please log in again.", "invalid_refresh_token");

    private static AuthResponseDto ToAuthResponse(User user, AccessToken accessToken, IssuedRefreshToken refreshToken) => new()
    {
        AccessToken = accessToken.Token,
        AccessTokenExpiresAt = accessToken.ExpiresAt,
        RefreshToken = refreshToken.Token,
        RefreshTokenExpiresAt = refreshToken.ExpiresAt,
        User = ToUserDto(user),
    };

    private static UserDto ToUserDto(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        IsEmailConfirmed = user.IsEmailConfirmed,
    };
}
