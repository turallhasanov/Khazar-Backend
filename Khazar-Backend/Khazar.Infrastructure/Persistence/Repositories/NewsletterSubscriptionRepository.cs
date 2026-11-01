using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

public sealed class NewsletterSubscriptionRepository(KhazarDbContext dbContext)
    : BaseRepository<NewsletterSubscription>(dbContext), INewsletterSubscriptionRepository
{
    /// <summary>Tracked, so reactivation and unsubscription are saved via UpdateAsync.</summary>
    public async Task<NewsletterSubscription?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(s => s.Email == email, cancellationToken);

    /// <summary>A concurrent sign-up of the same email is reported as "already subscribed", not a database error.</summary>
    public override async Task<NewsletterSubscription> AddAsync(
        NewsletterSubscription entity, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.AddAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueIndexViolation(NewsletterSubscriptionConfiguration.EmailIndexName))
        {
            throw new ConflictException("This email address is already subscribed to the newsletter.", "already_subscribed");
        }
    }
}
