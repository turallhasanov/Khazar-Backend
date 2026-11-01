using Khazar.Application.DTOs.Reviews;

namespace Khazar.Application.Services;

/// <summary>Writing, reading and moderating product reviews.</summary>
public interface IProductReviewAppService
{
    /// <summary>Approved reviews of the product and their rating statistics.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The product does not exist.</exception>
    Task<ProductReviewsResponseDto> GetApprovedReviewsAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a review for moderation. The author is marked as a verified buyer when they have ordered the product.
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">The rating or comment is invalid.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">The product does not exist.</exception>
    /// <exception cref="Common.Exceptions.UnauthorizedException">The account no longer exists.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The user has already reviewed this product.</exception>
    Task<AdminReviewDto> CreateReviewAsync(
        Guid productId, Guid userId, CreateReviewRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>The moderation queue, optionally filtered by status (Pending, Approved, Rejected).</summary>
    /// <exception cref="Common.Exceptions.ValidationException">The status is unknown.</exception>
    Task<IReadOnlyList<AdminReviewDto>> GetReviewsForModerationAsync(string? status, CancellationToken cancellationToken = default);

    /// <summary>Approves, rejects or resets a review to pending.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">The status is unknown.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">The review does not exist.</exception>
    Task<AdminReviewDto> UpdateStatusAsync(
        Guid reviewId, UpdateReviewStatusRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a review. Rejecting hides a review without losing it; this removes it for good.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The review does not exist.</exception>
    Task DeleteAsync(Guid reviewId, CancellationToken cancellationToken = default);
}
