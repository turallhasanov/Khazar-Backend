using Khazar.Application.DTOs.Content;
using Khazar.Application.DTOs.Newsletter;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>What shoppers send the shop, for platform admins: contact form messages and newsletter addresses.</summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin")]
public sealed class AdminInboxController(
    IContactMessageAppService contactMessageAppService,
    IAdminNewsletterAppService newsletterAppService) : BaseApiController
{
    // ---------- Contact messages ----------

    /// <summary>Returns every contact message, newest first.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    [HttpGet("contact-messages")]
    [ProducesResponseType<IReadOnlyList<ContactMessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContactMessagesAsync(CancellationToken cancellationToken) =>
        Ok(await contactMessageAppService.GetAllAsync(cancellationToken));

    /// <summary>Returns one contact message.</summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="404">No message has this id.</response>
    [HttpGet("contact-messages/{id:guid}")]
    [ProducesResponseType<ContactMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContactMessageAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await contactMessageAppService.GetByIdAsync(id, cancellationToken));

    /// <summary>Marks a message read or unread.</summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="dto">The new read state.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="404">No message has this id.</response>
    [HttpPatch("contact-messages/{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<ContactMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContactMessageAsync(
        [FromRoute] Guid id, [FromBody] UpdateContactMessageRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await contactMessageAppService.SetReadAsync(id, dto.IsRead, cancellationToken));

    /// <summary>Deletes a contact message.</summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">The message was deleted.</response>
    /// <response code="404">No message has this id.</response>
    [HttpDelete("contact-messages/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteContactMessageAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await contactMessageAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    // ---------- Newsletter subscribers ----------

    /// <summary>Returns every newsletter address, active and unsubscribed, newest subscription first.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    [HttpGet("newsletter-subscribers")]
    [ProducesResponseType<IReadOnlyList<NewsletterSubscriberDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNewsletterSubscribersAsync(CancellationToken cancellationToken) =>
        Ok(await newsletterAppService.GetAllAsync(cancellationToken));

    /// <summary>Unsubscribes or reactivates an address (no welcome email is sent).</summary>
    /// <param name="id">Subscription identifier.</param>
    /// <param name="dto">The new active state.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="404">No subscription has this id.</response>
    [HttpPatch("newsletter-subscribers/{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<NewsletterSubscriberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNewsletterSubscriberAsync(
        [FromRoute] Guid id, [FromBody] UpdateNewsletterSubscriberRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await newsletterAppService.SetActiveAsync(id, dto.IsActive, cancellationToken));

    /// <summary>Removes an address from the list completely.</summary>
    /// <param name="id">Subscription identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">The address was removed.</response>
    /// <response code="404">No subscription has this id.</response>
    [HttpDelete("newsletter-subscribers/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNewsletterSubscriberAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await newsletterAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
