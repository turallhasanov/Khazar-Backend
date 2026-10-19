using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>
/// A long-lived token that is exchanged for a new access token. Tokens are single-use: each refresh revokes the
/// presented token and issues a replacement. Only a hash of the token value is stored.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    /// <summary>Hash of the token issued in exchange for this one; null when revoked by logout or never used.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    // Required by ORMs
    private RefreshToken() { }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        UserId = Guard.NotEmpty(userId, nameof(UserId));
        TokenHash = Guard.NotEmpty(tokenHash, nameof(TokenHash));
        ExpiresAt = expiresAt;
    }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTime now) => !IsRevoked && now < ExpiresAt;

    public void Revoke(DateTime now, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
            return;

        RevokedAt = now;
        ReplacedByTokenHash = replacedByTokenHash;
        MarkUpdated();
    }
}
