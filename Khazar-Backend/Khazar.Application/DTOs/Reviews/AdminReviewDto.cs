namespace Khazar.Application.DTOs.Reviews;

/// <summary>A review in the moderation queue, with its author, status and product.</summary>
public sealed record AdminReviewDto : ProductReviewDto
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ProductSlug { get; init; } = string.Empty;
    public Guid UserId { get; init; }

    /// <summary>Pending, Approved or Rejected.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime? UpdatedAt { get; init; }
}
