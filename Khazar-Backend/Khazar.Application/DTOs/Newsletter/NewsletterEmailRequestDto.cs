namespace Khazar.Application.DTOs.Newsletter;

/// <summary>Body of the subscribe and unsubscribe requests.</summary>
public sealed record NewsletterEmailRequestDto
{
    public string Email { get; init; } = string.Empty;
}
