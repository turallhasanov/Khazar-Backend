namespace Khazar.Application.DTOs.Reviews;

/// <summary>A published review as shown on the product page. The author's account id is not exposed.</summary>
public record ProductReviewDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string Comment { get; init; } = string.Empty;
    public bool IsVerifiedBuyer { get; init; }
    public DateTime CreatedAt { get; init; }
}
