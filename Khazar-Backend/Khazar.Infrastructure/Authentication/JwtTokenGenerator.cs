using System.Security.Cryptography;
using System.Text;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Khazar.Infrastructure.Authentication;

/// <summary>
/// Issues HMAC-SHA256 signed access tokens and opaque random refresh tokens.
/// Registered as a singleton: the signing credentials are created once.
/// </summary>
public sealed class JwtTokenGenerator(JwtOptions options) : IJwtTokenGenerator
{
    private const int RefreshTokenBytes = 64;

    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _signingCredentials = new(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256);

    public AccessToken GenerateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(options.AccessTokenMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.GivenName] = user.FirstName,
                [JwtRegisteredClaimNames.FamilyName] = user.LastName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        });

        return new AccessToken(token, expiresAt);
    }

    public IssuedRefreshToken GenerateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        return new IssuedRefreshToken(token, HashRefreshToken(token), DateTime.UtcNow.AddDays(options.RefreshTokenDays));
    }

    // The token is 512 bits of randomness, so a fast unsalted hash is enough: there is nothing to brute-force.
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
