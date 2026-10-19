using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

/// <summary>User persistence contract. Emails are stored lower-case, so lookups expect a normalized address.</summary>
public interface IUserRepository : IAsyncRepository<User>
{
    /// <summary>Returns the tracked user with this email address, or null.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
}
