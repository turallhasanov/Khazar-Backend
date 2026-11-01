using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Domain.Enums;
using Khazar.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

public sealed class ProductReviewRepository(KhazarDbContext dbContext)
    : BaseRepository<ProductReview>(dbContext), IProductReviewRepository
{
    public async Task<IReadOnlyList<ProductReview>> GetApprovedByProductAsync(
        Guid productId, CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModeratedReview>> GetForModerationAsync(
        ReviewStatus? status, CancellationToken cancellationToken = default)
    {
        var reviews = Set.AsNoTracking();
        if (status is { } filter)
            reviews = reviews.Where(r => r.Status == filter);

        var rows = await JoinProducts(reviews)
            .OrderByDescending(row => row.Review.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(row => new ModeratedReview(row.Review, row.ProductName, row.ProductSlug)).ToList();
    }

    /// <summary>The review is tracked, so a status change is saved via UpdateAsync.</summary>
    public async Task<ModeratedReview?> GetForModerationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await JoinProducts(Set.Where(r => r.Id == id)).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new ModeratedReview(row.Review, row.ProductName, row.ProductSlug);
    }

    public async Task<bool> ExistsForUserAsync(Guid productId, Guid userId, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(r => r.ProductId == productId && r.UserId == userId, cancellationToken);

    /// <summary>A concurrent second review of the same product by the same user is reported as a conflict.</summary>
    public override async Task<ProductReview> AddAsync(ProductReview entity, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.AddAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueIndexViolation(ProductReviewConfiguration.ProductUserIndexName))
        {
            throw new ConflictException("You have already reviewed this product.", "review_already_exists");
        }
    }

    // Member-init projection (not the ModeratedReview constructor) so that EF can still order by the review's columns.
    private IQueryable<ReviewRow> JoinProducts(IQueryable<ProductReview> reviews) =>
        reviews.Join(
            DbContext.Products,
            review => review.ProductId,
            product => product.Id,
            (review, product) => new ReviewRow { Review = review, ProductName = product.Name, ProductSlug = product.Slug });

    private sealed class ReviewRow
    {
        public ProductReview Review { get; init; } = default!;
        public string ProductName { get; init; } = string.Empty;
        public string ProductSlug { get; init; } = string.Empty;
    }
}
