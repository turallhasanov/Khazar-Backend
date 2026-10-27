using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>
/// An email address on the newsletter list. Unsubscribing keeps the row (inactive) so that a later subscription
/// reactivates it instead of creating a duplicate.
/// </summary>
public class NewsletterSubscription : BaseEntity
{
    public string Email { get; private set; } = default!;
    public bool IsActive { get; private set; }

    /// <summary>Start of the current subscription; reset when an inactive address subscribes again.</summary>
    public DateTime SubscribedAt { get; private set; }

    /// <summary>When the address last unsubscribed; null while the subscription is active.</summary>
    public DateTime? UnsubscribedAt { get; private set; }

    // Required by ORMs
    private NewsletterSubscription() { }

    public NewsletterSubscription(string email, DateTime now)
    {
        Email = Guard.Email(email, nameof(Email));
        IsActive = true;
        SubscribedAt = now;
    }

    /// <summary>Reactivates an unsubscribed address. Returns false when it was already active.</summary>
    public bool Resubscribe(DateTime now)
    {
        if (IsActive)
            return false;

        IsActive = true;
        SubscribedAt = now;
        UnsubscribedAt = null;
        MarkUpdated();
        return true;
    }

    /// <summary>Stops the subscription. Returns false when it was already inactive.</summary>
    public bool Unsubscribe(DateTime now)
    {
        if (!IsActive)
            return false;

        IsActive = false;
        UnsubscribedAt = now;
        MarkUpdated();
        return true;
    }
}
