using Khazar.Application.DTOs.Newsletter;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>Newsletter sign-up from the storefront footer.</summary>
[EnableRateLimiting(AuthenticationExtensions.PublicFormRateLimitPolicy)]
[Route("api/khazar")]
public sealed class NewsletterController(INewsletterAppService newsletterAppService) : BaseApiController
{
    /// <summary>Subscribes an email address, or reactivates one that had unsubscribed.</summary>
    /// <param name="dto">The email address.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The address was added to the list.</response>
    /// <response code="200">A previously unsubscribed address was reactivated (reactivated: true).</response>
    /// <response code="400">The email address is invalid ("invalid_email").</response>
    /// <response code="409">The address is already subscribed ("already_subscribed").</response>
    /// <response code="429">Too many requests from this client.</response>
    [HttpPost("subscribe")]
    [Consumes("application/json")]
    [ProducesResponseType<NewsletterSubscriptionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<NewsletterSubscriptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SubscribeAsync([FromBody] NewsletterEmailRequestDto dto, CancellationToken cancellationToken)
    {
        var subscription = await newsletterAppService.SubscribeAsync(dto, cancellationToken);
        return subscription.Reactivated ? Ok(subscription) : StatusCode(StatusCodes.Status201Created, subscription);
    }

    /// <summary>Stops the newsletter for an email address.</summary>
    /// <param name="dto">The email address.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">Unsubscribed. Returned for unknown and already unsubscribed addresses too.</response>
    /// <response code="400">The email address is invalid ("invalid_email").</response>
    /// <response code="429">Too many requests from this client.</response>
    [HttpPost("unsubscribe")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UnsubscribeAsync([FromBody] NewsletterEmailRequestDto dto, CancellationToken cancellationToken)
    {
        await newsletterAppService.UnsubscribeAsync(dto, cancellationToken);
        return NoContent();
    }
}
