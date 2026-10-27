namespace Khazar.Application.DTOs.Reviews;

public sealed record UpdateReviewStatusRequestDto
{
    /// <summary>Pending, Approved or Rejected (case-insensitive).</summary>
    public string Status { get; init; } = string.Empty;
}
