using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(KhazarDbContext dbContext)
    : BaseRepository<RefreshToken>(dbContext), IRefreshTokenRepository
{
    /// <summary>Tracked, so a rotation (revoke + add) is committed by the following AddAsync in one save.</summary>
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default)
    {
        var active = await Set
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
            token.Revoke(now);

        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
