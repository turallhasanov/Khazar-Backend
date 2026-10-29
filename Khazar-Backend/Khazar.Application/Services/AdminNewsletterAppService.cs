using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Newsletter;
using Khazar.Domain.Entities;

namespace Khazar.Application.Services;

public sealed class AdminNewsletterAppService(INewsletterSubscriptionRepository subscriptionRepository) : IAdminNewsletterAppService
{
    public async Task<IReadOnlyList<NewsletterSubscriberDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await subscriptionRepository.GetAllAsync(cancellationToken))
            .OrderByDescending(subscription => subscription.SubscribedAt)
            .Select(ToDto)
            .ToList();

    public async Task<NewsletterSubscriberDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAsync(id, cancellationToken);
        var now = DateTime.UtcNow;
        var changed = isActive ? subscription.Resubscribe(now) : subscription.Unsubscribe(now);
        if (changed)
            await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        return ToDto(subscription);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await subscriptionRepository.DeleteAsync(await LoadAsync(id, cancellationToken), cancellationToken);

    private async Task<NewsletterSubscription> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await subscriptionRepository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException(nameof(NewsletterSubscription), id);

    private static NewsletterSubscriberDto ToDto(NewsletterSubscription subscription) => new()
    {
        Id = subscription.Id,
        Email = subscription.Email,
        IsActive = subscription.IsActive,
        SubscribedAt = subscription.SubscribedAt,
        UnsubscribedAt = subscription.UnsubscribedAt,
        CreatedAt = subscription.CreatedAt,
    };
}
