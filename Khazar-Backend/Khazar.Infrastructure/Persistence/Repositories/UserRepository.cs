using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(KhazarDbContext dbContext)
    : BaseRepository<User>(dbContext), IUserRepository
{
    /// <summary>Tracked, so confirmation attempts and code changes are saved via UpdateAsync.</summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(u => u.Email == email, cancellationToken);

    /// <summary>A concurrent registration of the same email is reported as a conflict, not a database error.</summary>
    public override async Task<User> AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.AddAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueIndexViolation(UserConfiguration.EmailIndexName))
        {
            throw new ConflictException("An account with this email address already exists.", "email_already_registered");
        }
    }
}
