using System.Security.Cryptography;
using System.Text;
using Khazar.Domain.Common;
using Khazar.Domain.Enums;
using Khazar.Domain.Exceptions;

namespace Khazar.Domain.Entities;

/// <summary>
/// A storefront customer account. Sign-in is allowed only after the email address is confirmed with the
/// one-time code sent at registration.
/// </summary>
public class User : BaseEntity
{
    /// <summary>Wrong codes allowed per issued code; afterwards a new code must be requested.</summary>
    public const int MaxConfirmationAttempts = 5;

    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsEmailConfirmed { get; private set; }
    public DateTime? EmailConfirmedAt { get; private set; }

    /// <summary>Hash of the pending email confirmation code; the code itself is never stored.</summary>
    public string? ConfirmationToken { get; private set; }

    public DateTime? ConfirmationTokenExpiresAt { get; private set; }
    public int ConfirmationAttempts { get; private set; }

    /// <summary>Hash of the pending password reset token; the token itself is only sent by email.</summary>
    public string? PasswordResetTokenHash { get; private set; }

    public DateTime? PasswordResetTokenExpiresAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    // Required by ORMs
    private User() { }

    public User(string firstName, string lastName, string email, string passwordHash)
    {
        FirstName = Guard.NotEmpty(firstName, nameof(FirstName));
        LastName = Guard.NotEmpty(lastName, nameof(LastName));
        Email = Guard.Email(email, nameof(Email));
        PasswordHash = Guard.NotEmpty(passwordHash, nameof(PasswordHash));
    }

    /// <summary>Stores a new confirmation code hash; any previous code stops working.</summary>
    public void SetConfirmationToken(string tokenHash, DateTime expiresAt)
    {
        if (IsEmailConfirmed)
            throw new DomainException("The email address is already confirmed.");

        ConfirmationToken = Guard.NotEmpty(tokenHash, nameof(ConfirmationToken));
        ConfirmationTokenExpiresAt = expiresAt;
        ConfirmationAttempts = 0;
        MarkUpdated();
    }

    /// <summary>
    /// Checks a confirmation code hash and confirms the email address when it matches. A wrong code counts as an
    /// attempt, so the caller must persist the user whatever the result.
    /// </summary>
    public EmailConfirmationResult ConfirmEmail(string tokenHash, DateTime now)
    {
        if (IsEmailConfirmed)
            return EmailConfirmationResult.AlreadyConfirmed;

        if (ConfirmationToken is null || ConfirmationTokenExpiresAt is null || now >= ConfirmationTokenExpiresAt)
            return EmailConfirmationResult.Expired;

        if (ConfirmationAttempts >= MaxConfirmationAttempts)
            return EmailConfirmationResult.TooManyAttempts;

        // Constant-time comparison: response timing must not reveal how much of the hash matched.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(ConfirmationToken), Encoding.ASCII.GetBytes(tokenHash ?? string.Empty)))
        {
            ConfirmationAttempts++;
            MarkUpdated();
            return ConfirmationAttempts >= MaxConfirmationAttempts
                ? EmailConfirmationResult.TooManyAttempts
                : EmailConfirmationResult.InvalidCode;
        }

        IsEmailConfirmed = true;
        EmailConfirmedAt = now;
        ConfirmationToken = null;
        ConfirmationTokenExpiresAt = null;
        ConfirmationAttempts = 0;
        MarkUpdated();
        return EmailConfirmationResult.Confirmed;
    }

    /// <summary>
    /// Confirms the email address without a code. Only for accounts created by the system itself (the seeded
    /// SuperAdmin), whose address is set by configuration rather than typed in by a visitor.
    /// </summary>
    public void MarkEmailConfirmed(DateTime now)
    {
        if (IsEmailConfirmed)
            return;

        IsEmailConfirmed = true;
        EmailConfirmedAt = now;
        ConfirmationToken = null;
        ConfirmationTokenExpiresAt = null;
        ConfirmationAttempts = 0;
        MarkUpdated();
    }

    /// <summary>Stores a new password reset token hash; any previously issued reset link stops working.</summary>
    public void SetPasswordResetToken(string tokenHash, DateTime expiresAt)
    {
        PasswordResetTokenHash = Guard.NotEmpty(tokenHash, nameof(PasswordResetTokenHash));
        PasswordResetTokenExpiresAt = expiresAt;
        MarkUpdated();
    }

    /// <summary>
    /// Replaces the password when the reset token hash matches and has not expired. The token is single-use:
    /// it is cleared on success. Returns false for a wrong, expired or already used token.
    /// </summary>
    public bool ResetPassword(string tokenHash, string newPasswordHash, DateTime now)
    {
        if (PasswordResetTokenHash is null || PasswordResetTokenExpiresAt is null || now >= PasswordResetTokenExpiresAt)
            return false;

        // Constant-time comparison: response timing must not reveal how much of the hash matched.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(PasswordResetTokenHash), Encoding.ASCII.GetBytes(tokenHash ?? string.Empty)))
            return false;

        PasswordHash = Guard.NotEmpty(newPasswordHash, nameof(PasswordHash));
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAt = null;
        MarkUpdated();
        return true;
    }
}
