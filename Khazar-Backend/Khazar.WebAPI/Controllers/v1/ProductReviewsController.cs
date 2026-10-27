using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Reviews;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Storefront product reviews. Submitted reviews are published only after a platform admin approves them
/// (see /api/admin/reviews).
/// </summary>
[Route("api/khazar/products/{productId:guid}/reviews")]
public sealed class ProductReviewsController(IProductReviewAppService reviewAppService, ICurrentUserService currentUser)
    : BaseApiController
{
    /// <summary>Returns the approved reviews of a product (newest first) with the average rating and star distribution.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">Reviews and rating statistics. A product without reviews has averageRating 0.</response>
    /// <response code="404">No product has this id.</response>
    [HttpGet]
    [ProducesResponseType<ProductReviewsResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductReviewsAsync([FromRoute] Guid productId, CancellationToken cancellationToken) =>
        Ok(await reviewAppService.GetApprovedReviewsAsync(productId, cancellationToken));

    /// <summary>
    /// Submits a review for moderation. isVerifiedBuyer is set when the signed-in user has ordered the product.
    /// </summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="dto">Rating (1-5) and comment (at most 2000 characters).</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The review was saved with status Pending.</response>
    /// <response code="400">The rating ("invalid_rating") or comment ("invalid_comment") is invalid.</response>
    /// <response code="401">No valid access token was sent.</response>
    /// <response code="404">No product has this id.</response>
    /// <response code="409">The user has already reviewed this product ("review_already_exists").</response>
    /// <response code="429">Too many submissions from this client.</response>
    [Authorize]
    [EnableRateLimiting(AuthenticationExtensions.PublicFormRateLimitPolicy)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<AdminReviewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateProductReviewAsync(
        [FromRoute] Guid productId, [FromBody] CreateReviewRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Sign in to write a review.");
        var review = await reviewAppService.CreateReviewAsync(productId, userId, dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, review);
    }
}
