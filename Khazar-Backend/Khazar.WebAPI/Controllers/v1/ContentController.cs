using Khazar.Application.Common.Exceptions;
using Khazar.Application.DTOs.Content;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Storefront content managed in the admin panel: page copy, lookbook, journal and FAQ, plus the contact form.
/// Responses are not cached, so a change saved in the admin panel shows on the next load.
/// </summary>
[Route("api")]
public sealed class ContentController(
    ISiteContentAppService contentAppService,
    IContactMessageAppService contactMessageAppService) : BaseApiController
{
    /// <summary>Returns every saved section with the published looks, article summaries and FAQ items.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The storefront content. Sections that were never saved are absent.</response>
    [HttpGet("content")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<SiteContentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContentAsync(CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetSiteContentAsync(cancellationToken));

    /// <summary>Returns a published journal article with its body.</summary>
    /// <param name="slug">The article's slug, as in /journal/{slug}.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The article.</response>
    /// <response code="404">No published article has this slug.</response>
    [HttpGet("content/journal/{slug}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<JournalArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJournalArticleAsync([FromRoute] string slug, CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetPublishedArticleAsync(slug, cancellationToken)
           ?? throw new NotFoundException($"No published article has the slug \"{slug}\"."));

    /// <summary>Sends a message from the contact page to the admin inbox. Limited to 10 requests per minute per IP.</summary>
    /// <param name="dto">Name, email and message.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The message was stored.</response>
    /// <response code="400">A field is missing or too long, or the email is invalid ("invalid_content").</response>
    /// <response code="429">Too many messages from this address; try again in a minute.</response>
    [HttpPost("contact-messages")]
    [EnableRateLimiting(AuthenticationExtensions.PublicFormRateLimitPolicy)]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendContactMessageAsync(
        [FromBody] CreateContactMessageRequestDto dto, CancellationToken cancellationToken)
    {
        // The message itself is not echoed back: the sender only needs to know it arrived.
        await contactMessageAppService.SubmitAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created);
    }
}
