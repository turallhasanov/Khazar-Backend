using Khazar.Application.DTOs.Reviews;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>Review moderation for platform admins (accounts listed in "Admin:Emails").</summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin/reviews")]
public sealed class AdminReviewsController(IProductReviewAppService reviewAppService) : BaseApiController
{
    /// <summary>Returns reviews of every status (newest first), optionally filtered by status.</summary>
    /// <param name="status">Pending, Approved or Rejected (case-insensitive). Omit it to get all reviews.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The matching reviews with their product and author.</response>
    /// <response code="400">The status is unknown ("invalid_review_status").</response>
    /// <response code="401">No valid access token was sent.</response>
    /// <response code="403">The signed-in user is not a platform admin.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminReviewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReviewsAsync([FromQuery] string? status, CancellationToken cancellationToken) =>
        Ok(await reviewAppService.GetReviewsForModerationAsync(status, cancellationToken));

    /// <summary>Approves or rejects a review (or moves it back to Pending). Only approved reviews are published.</summary>
    /// <param name="id">Review identifier.</param>
    /// <param name="dto">The new status.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The updated review.</response>
    /// <response code="400">The status is unknown ("invalid_review_status").</response>
    /// <response code="401">No valid access token was sent.</response>
    /// <response code="403">The signed-in user is not a platform admin.</response>
    /// <response code="404">No review has this id.</response>
    [HttpPatch("{id:guid}/status")]
    [Consumes("application/json")]
    [ProducesResponseType<AdminReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReviewStatusAsync(
        [FromRoute] Guid id, [FromBody] UpdateReviewStatusRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await reviewAppService.UpdateStatusAsync(id, dto, cancellationToken));

    /// <summary>Permanently deletes a review. To only hide it from the storefront, reject it instead.</summary>
    /// <param name="id">Review identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">The review was deleted.</response>
    /// <response code="401">No valid access token was sent.</response>
    /// <response code="403">The signed-in user is not a platform admin.</response>
    /// <response code="404">No review has this id.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReviewAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await reviewAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
