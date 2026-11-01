using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

public interface IRefreshTokenRepository : IAsyncRepository<RefreshToken>
{
    /// <summary>Returns the tracked token with this hash, or null.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active token of the user, e.g. after a revoked token was presented again.</summary>
    Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default);
}
