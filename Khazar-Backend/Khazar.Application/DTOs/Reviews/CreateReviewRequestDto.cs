namespace Khazar.Application.DTOs.Reviews;

public sealed record CreateReviewRequestDto
{
    /// <summary>Star rating from 1 to 5.</summary>
    public int Rating { get; init; }

    /// <summary>Review text, at most 2000 characters.</summary>
    public string Comment { get; init; } = string.Empty;
}
