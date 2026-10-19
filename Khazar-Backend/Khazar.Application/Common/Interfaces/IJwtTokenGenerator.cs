using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

/// <summary>Issues the access and refresh tokens returned by the auth endpoints.</summary>
public interface IJwtTokenGenerator
{
    /// <summary>Creates a signed JWT carrying the user's id, email and name.</summary>
    AccessToken GenerateAccessToken(User user);

    /// <summary>Creates a random refresh token. Only <see cref="IssuedRefreshToken.TokenHash"/> may be stored.</summary>
    IssuedRefreshToken GenerateRefreshToken();

    /// <summary>Hashes a refresh token presented by a client, for lookup by <see cref="RefreshToken.TokenHash"/>.</summary>
    string HashRefreshToken(string refreshToken);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public sealed record IssuedRefreshToken(string Token, string TokenHash, DateTime ExpiresAt);
