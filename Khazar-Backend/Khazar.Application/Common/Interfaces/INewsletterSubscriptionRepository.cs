using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

/// <summary>Newsletter subscription persistence contract. Emails are stored lower-case, so lookups expect a normalized address.</summary>
public interface INewsletterSubscriptionRepository : IAsyncRepository<NewsletterSubscription>
{
    /// <summary>Returns the tracked subscription for this email address, or null.</summary>
    Task<NewsletterSubscription?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
