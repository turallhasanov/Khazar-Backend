using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Newsletter;
using Khazar.Domain.Entities;

namespace Khazar.Application.Services;

public sealed class NewsletterAppService(
    INewsletterSubscriptionRepository subscriptionRepository,
    IBackgroundEmailQueue emailQueue) : INewsletterAppService
{
    private const int MaxEmailLength = 320;

    public async Task<NewsletterSubscriptionDto> SubscribeAsync(
        NewsletterEmailRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var email = NormalizeEmail(dto.Email);
        var now = DateTime.UtcNow;
        var subscription = await subscriptionRepository.GetByEmailAsync(email, cancellationToken);

        if (subscription is null)
        {
            // A concurrent sign-up of the same address is reported as a ConflictException by the repository.
            subscription = await subscriptionRepository.AddAsync(new NewsletterSubscription(email, now), cancellationToken);
            SendWelcome(email);
            return ToDto(subscription, reactivated: false);
        }

        if (!subscription.Resubscribe(now))
            throw new ConflictException("This email address is already subscribed to the newsletter.", "already_subscribed");

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        SendWelcome(email);
        return ToDto(subscription, reactivated: true);
    }

    // Only after the subscription is saved; a mail server failure never undoes or fails the sign-up.
    private void SendWelcome(string email) =>
        emailQueue.Enqueue($"newsletter welcome for {email}", (sender, ct) => sender.SendNewsletterWelcomeAsync(email, ct));

    public async Task UnsubscribeAsync(NewsletterEmailRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Unknown addresses get the same answer, so the endpoint does not reveal who is on the list.
        var subscription = await subscriptionRepository.GetByEmailAsync(NormalizeEmail(dto.Email), cancellationToken);
        if (subscription is not null && subscription.Unsubscribe(DateTime.UtcNow))
            await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
    }

    // Same shape rules as the domain (one '@' with text on both sides), reported as a 400 validation error.
    private static string NormalizeEmail(string? value)
    {
        var email = value?.Trim();
        if (string.IsNullOrEmpty(email))
            throw new ValidationException("Email is required.", "invalid_email");

        var at = email.IndexOf('@');
        if (email.Length > MaxEmailLength || at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
            throw new ValidationException("Invalid email address.", "invalid_email");

        return email.ToLowerInvariant();
    }

    private static NewsletterSubscriptionDto ToDto(NewsletterSubscription subscription, bool reactivated) => new()
    {
        Email = subscription.Email,
        IsActive = subscription.IsActive,
        SubscribedAt = subscription.SubscribedAt,
        Reactivated = reactivated,
    };
}
