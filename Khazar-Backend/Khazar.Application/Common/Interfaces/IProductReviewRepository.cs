using Khazar.Domain.Entities;
using Khazar.Domain.Enums;

namespace Khazar.Application.Common.Interfaces;

/// <summary>
/// Product review persistence contract. GetByIdAsync returns a tracked review for status changes. AddAsync reports a
/// second review of the same product by the same user (e.g. a concurrent duplicate request) as a
/// <see cref="Exceptions.ConflictException"/>.
/// </summary>
public interface IProductReviewRepository : IAsyncRepository<ProductReview>
{
    /// <summary>Approved reviews of the product, newest first.</summary>
    Task<IReadOnlyList<ProductReview>> GetApprovedByProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Reviews for the moderation queue with their product, newest first; all statuses when <paramref name="status"/> is null.</summary>
    Task<IReadOnlyList<ModeratedReview>> GetForModerationAsync(ReviewStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Returns the review together with its product, or null.</summary>
    Task<ModeratedReview?> GetForModerationByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsForUserAsync(Guid productId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>A review with the name and slug of the reviewed product, for the admin moderation list.</summary>
public sealed record ModeratedReview(ProductReview Review, string ProductName, string ProductSlug);
