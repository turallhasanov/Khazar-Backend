using Khazar.Application.DTOs.Newsletter;

namespace Khazar.Application.Services;

/// <summary>Newsletter sign-up and opt-out.</summary>
public interface INewsletterAppService
{
    /// <summary>Adds the address, or reactivates it when it had unsubscribed (<see cref="NewsletterSubscriptionDto.Reactivated"/>).</summary>
    /// <exception cref="Common.Exceptions.ValidationException">The email address is invalid.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The address is already subscribed ("already_subscribed").</exception>
    Task<NewsletterSubscriptionDto> SubscribeAsync(NewsletterEmailRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Stops the subscription. Unknown and already unsubscribed addresses are accepted silently.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">The email address is invalid.</exception>
    Task UnsubscribeAsync(NewsletterEmailRequestDto dto, CancellationToken cancellationToken = default);
}
