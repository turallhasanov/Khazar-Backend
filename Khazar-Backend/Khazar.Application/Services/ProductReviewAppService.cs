using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Reviews;
using Khazar.Domain.Entities;
using Khazar.Domain.Enums;

namespace Khazar.Application.Services;

public sealed class ProductReviewAppService(
    IProductReviewRepository reviewRepository,
    IProductRepository productRepository,
    IOrderRepository orderRepository,
    IUserRepository userRepository) : IProductReviewAppService
{
    public async Task<ProductReviewsResponseDto> GetApprovedReviewsAsync(
        Guid productId, CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);

        var reviews = await reviewRepository.GetApprovedByProductAsync(productId, cancellationToken);

        var distribution = Enumerable.Range(ProductReview.MinRating, ProductReview.MaxRating)
            .ToDictionary(rating => rating, rating => reviews.Count(r => r.Rating == rating));

        return new ProductReviewsResponseDto
        {
            ProductId = productId,
            AverageRating = reviews.Count == 0 ? 0 : Math.Round(reviews.Average(r => r.Rating), 1),
            ReviewCount = reviews.Count,
            RatingDistribution = distribution,
            Reviews = reviews.Select(ToPublicDto).ToList(),
        };
    }

    public async Task<AdminReviewDto> CreateReviewAsync(
        Guid productId, Guid userId, CreateReviewRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Rating is < ProductReview.MinRating or > ProductReview.MaxRating)
            throw new ValidationException(
                $"Rating must be between {ProductReview.MinRating} and {ProductReview.MaxRating}.", "invalid_rating");

        var comment = dto.Comment?.Trim();
        if (string.IsNullOrEmpty(comment))
            throw new ValidationException("Comment is required.", "invalid_comment");
        if (comment.Length > ProductReview.MaxCommentLength)
            throw new ValidationException(
                $"Comment must be at most {ProductReview.MaxCommentLength} characters.", "invalid_comment");

        // Archived products take no new reviews.
        var product = await productRepository.GetByIdAsync(productId, cancellationToken) is { IsActive: true } active
            ? active
            : throw new NotFoundException("Product", productId);

        // A valid token of a deleted account.
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("The account no longer exists.");

        if (await reviewRepository.ExistsForUserAsync(productId, userId, cancellationToken))
            throw new ConflictException("You have already reviewed this product.", "review_already_exists");

        var isVerifiedBuyer = await orderRepository.HasPurchasedProductAsync(user.Id, user.Email, productId, cancellationToken);

        var review = new ProductReview(productId, user.Id, DisplayName(user), dto.Rating, comment, isVerifiedBuyer);
        await reviewRepository.AddAsync(review, cancellationToken);

        return ToAdminDto(new ModeratedReview(review, product.Name, product.Slug));
    }

    public async Task<IReadOnlyList<AdminReviewDto>> GetReviewsForModerationAsync(
        string? status, CancellationToken cancellationToken = default)
    {
        ReviewStatus? filter = string.IsNullOrWhiteSpace(status) ? null : ParseStatus(status);
        var reviews = await reviewRepository.GetForModerationAsync(filter, cancellationToken);
        return reviews.Select(ToAdminDto).ToList();
    }

    public async Task<AdminReviewDto> UpdateStatusAsync(
        Guid reviewId, UpdateReviewStatusRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var status = ParseStatus(dto.Status);
        var moderated = await reviewRepository.GetForModerationByIdAsync(reviewId, cancellationToken)
            ?? throw new NotFoundException("Review", reviewId);

        moderated.Review.SetStatus(status);
        await reviewRepository.UpdateAsync(moderated.Review, cancellationToken);

        return ToAdminDto(moderated);
    }

    public async Task DeleteAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await reviewRepository.GetByIdAsync(reviewId, cancellationToken)
            ?? throw new NotFoundException("Review", reviewId);

        await reviewRepository.DeleteAsync(review, cancellationToken);
    }

    private async Task EnsureProductExistsAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (await productRepository.GetByIdAsync(productId, cancellationToken) is null)
            throw new NotFoundException("Product", productId);
    }

    // Names only (no numeric values such as "1"), so a typo cannot silently map to a status.
    private static ReviewStatus ParseStatus(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0])
            || !Enum.TryParse<ReviewStatus>(name, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            throw new ValidationException(
                $"Status must be one of: {string.Join(", ", Enum.GetNames<ReviewStatus>())}.", "invalid_review_status");
        }

        return status;
    }

    // First name and last initial, e.g. "Tural H.": the full surname is not published next to the review.
    private static string DisplayName(User user) => $"{user.FirstName} {char.ToUpperInvariant(user.LastName[0])}.";

    private static ProductReviewDto ToPublicDto(ProductReview review) => new()
    {
        Id = review.Id,
        UserName = review.UserName,
        Rating = review.Rating,
        Comment = review.Comment,
        IsVerifiedBuyer = review.IsVerifiedBuyer,
        CreatedAt = review.CreatedAt,
    };

    private static AdminReviewDto ToAdminDto(ModeratedReview moderated)
    {
        var review = moderated.Review;
        return new AdminReviewDto
        {
            Id = review.Id,
            UserName = review.UserName,
            Rating = review.Rating,
            Comment = review.Comment,
            IsVerifiedBuyer = review.IsVerifiedBuyer,
            CreatedAt = review.CreatedAt,
            ProductId = review.ProductId,
            ProductName = moderated.ProductName,
            ProductSlug = moderated.ProductSlug,
            UserId = review.UserId,
            Status = review.Status.ToString(),
            UpdatedAt = review.UpdatedAt,
        };
    }
}
