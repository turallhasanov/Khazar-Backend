namespace Khazar.Application.DTOs.Reviews;

/// <summary>Approved reviews of a product with the rating statistics computed from them.</summary>
public sealed record ProductReviewsResponseDto
{
    public Guid ProductId { get; init; }

    /// <summary>Mean of the approved ratings rounded to one decimal; 0 when there are no reviews.</summary>
    public double AverageRating { get; init; }

    public int ReviewCount { get; init; }

    /// <summary>Number of approved reviews per star rating, keyed "1" to "5"; every key is present.</summary>
    public IReadOnlyDictionary<int, int> RatingDistribution { get; init; } = new Dictionary<int, int>();

    public IReadOnlyList<ProductReviewDto> Reviews { get; init; } = [];
}
