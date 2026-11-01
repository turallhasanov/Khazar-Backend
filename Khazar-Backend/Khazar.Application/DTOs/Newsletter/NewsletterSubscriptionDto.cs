namespace Khazar.Application.DTOs.Newsletter;

public sealed record NewsletterSubscriptionDto
{
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime SubscribedAt { get; init; }

    /// <summary>True when a previously unsubscribed address was reactivated instead of newly added.</summary>
    public bool Reactivated { get; init; }
}
