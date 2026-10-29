namespace Khazar.Application.DTOs.Newsletter;

/// <summary>A newsletter address as the admin panel lists it.</summary>
public sealed record NewsletterSubscriberDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime SubscribedAt { get; init; }
    public DateTime? UnsubscribedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record UpdateNewsletterSubscriberRequestDto
{
    public bool IsActive { get; init; }
}
