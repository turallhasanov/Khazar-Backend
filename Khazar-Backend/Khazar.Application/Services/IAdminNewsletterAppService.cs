using Khazar.Application.DTOs.Newsletter;

namespace Khazar.Application.Services;

/// <summary>The newsletter list for the admin panel: view, unsubscribe or reactivate, and delete addresses.</summary>
public interface IAdminNewsletterAppService
{
    /// <summary>Every address, active and unsubscribed, newest subscription first.</summary>
    Task<IReadOnlyList<NewsletterSubscriberDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Unsubscribes or reactivates an address. Reactivating does not send the welcome email again.</summary>
    Task<NewsletterSubscriberDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Removes the address completely, e.g. on an erasure request.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
