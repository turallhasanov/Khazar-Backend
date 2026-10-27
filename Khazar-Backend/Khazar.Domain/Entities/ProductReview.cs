using Khazar.Domain.Common;
using Khazar.Domain.Enums;

namespace Khazar.Domain.Entities;

/// <summary>
/// A customer's rating and comment on a product. New reviews wait for moderation and appear on the storefront
/// only once a platform admin approves them. One review per user and product.
/// </summary>
public class ProductReview : BaseEntity
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 2000;
    public const int MaxUserNameLength = 120;

    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Display name snapshot taken when the review was written, e.g. "Tural H.".</summary>
    public string UserName { get; private set; } = default!;

    public int Rating { get; private set; }
    public string Comment { get; private set; } = default!;
    public ReviewStatus Status { get; private set; }

    /// <summary>True when the author had ordered this product before writing the review.</summary>
    public bool IsVerifiedBuyer { get; private set; }

    // Required by ORMs
    private ProductReview() { }

    public ProductReview(Guid productId, Guid userId, string userName, int rating, string comment, bool isVerifiedBuyer)
    {
        ProductId = Guard.NotEmpty(productId, nameof(ProductId));
        UserId = Guard.NotEmpty(userId, nameof(UserId));
        UserName = Guard.MaxLength(Guard.NotEmpty(userName, nameof(UserName)), MaxUserNameLength, nameof(UserName));
        Rating = Guard.InRange(rating, MinRating, MaxRating, nameof(Rating));
        Comment = Guard.MaxLength(Guard.NotEmpty(comment, nameof(Comment)), MaxCommentLength, nameof(Comment));
        IsVerifiedBuyer = isVerifiedBuyer;
        Status = ReviewStatus.Pending;
    }

    /// <summary>Sets the moderation state. Admins may also move a review back to Pending or revise a decision.</summary>
    public void SetStatus(ReviewStatus status)
    {
        if (Status == status)
            return;

        Status = status;
        MarkUpdated();
    }
}
