using System.Text.Json;
using Khazar.Application.DTOs.Content;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Storefront content management for platform admins: page sections, journal articles, lookbook looks and FAQ items.
/// Changes are visible on the storefront's next load of GET /api/content. Errors carry an "errorCode":
/// invalid_content (the detail names the field), invalid_slug, article_slug_taken, invalid_hotspot_product.
/// </summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin")]
public sealed class AdminContentController(IAdminContentAppService contentAppService) : BaseApiController
{
    // ---------- Sections ----------

    /// <summary>Returns every content section with its fields; never-saved ones have empty fields and no updatedAt.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">All sections.</response>
    [HttpGet("content/sections")]
    [ProducesResponseType<IReadOnlyList<ContentSectionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSectionsAsync(CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetSectionsAsync(cancellationToken));

    /// <summary>Replaces a section's fields (send the whole section, as GET returns it).</summary>
    /// <param name="key">Section key, e.g. "hero" or "about".</param>
    /// <param name="data">The section's fields.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The saved section.</response>
    /// <response code="400">A field is unknown, too long or not a safe link ("invalid_content").</response>
    /// <response code="404">No section has this key.</response>
    [HttpPut("content/sections/{key}")]
    [Consumes("application/json")]
    [ProducesResponseType<ContentSectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveSectionAsync(
        [FromRoute] string key, [FromBody] JsonElement data, CancellationToken cancellationToken) =>
        Ok(await contentAppService.SaveSectionAsync(key, data, cancellationToken));

    // ---------- Journal articles ----------

    /// <summary>Returns every article, unpublished ones included, in home page order.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    [HttpGet("journal-articles")]
    [ProducesResponseType<IReadOnlyList<JournalArticleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArticlesAsync(CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetArticlesAsync(cancellationToken));

    /// <summary>Returns one article with its body.</summary>
    /// <param name="id">Article identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="404">No article has this id.</response>
    [HttpGet("journal-articles/{id:guid}", Name = "GetAdminJournalArticle")]
    [ProducesResponseType<JournalArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticleAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetArticleAsync(id, cancellationToken));

    /// <summary>Creates an article.</summary>
    /// <param name="dto">Slug, title, excerpt, cover image, meta, sections, order and published flag.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The created article.</response>
    /// <response code="400">A field is invalid (see errorCode).</response>
    /// <response code="409">The slug is already used ("article_slug_taken").</response>
    [HttpPost("journal-articles")]
    [Consumes("application/json")]
    [ProducesResponseType<JournalArticleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateArticleAsync([FromBody] SaveJournalArticleRequestDto dto, CancellationToken cancellationToken)
    {
        var created = await contentAppService.CreateArticleAsync(dto, cancellationToken);
        return CreatedAtRoute("GetAdminJournalArticle", new { id = created.Id }, created);
    }

    /// <summary>Updates an article.</summary>
    /// <param name="id">Article identifier.</param>
    /// <param name="dto">The new field values.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="400">A field is invalid (see errorCode).</response>
    /// <response code="404">No article has this id.</response>
    /// <response code="409">The slug is already used ("article_slug_taken").</response>
    [HttpPut("journal-articles/{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<JournalArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateArticleAsync(
        [FromRoute] Guid id, [FromBody] SaveJournalArticleRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await contentAppService.UpdateArticleAsync(id, dto, cancellationToken));

    /// <summary>Deletes an article; its /journal/{slug} page stops working.</summary>
    /// <param name="id">Article identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">The article was deleted.</response>
    /// <response code="404">No article has this id.</response>
    [HttpDelete("journal-articles/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteArticleAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await contentAppService.DeleteArticleAsync(id, cancellationToken);
        return NoContent();
    }

    // ---------- Lookbook looks ----------

    /// <summary>Returns every look, unpublished ones included, in lookbook order.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    [HttpGet("lookbook-looks")]
    [ProducesResponseType<IReadOnlyList<LookbookLookDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLooksAsync(CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetLooksAsync(cancellationToken));

    /// <summary>Returns one look.</summary>
    /// <param name="id">Look identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="404">No look has this id.</response>
    [HttpGet("lookbook-looks/{id:guid}", Name = "GetAdminLookbookLook")]
    [ProducesResponseType<LookbookLookDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLookAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetLookAsync(id, cancellationToken));

    /// <summary>Creates a look.</summary>
    /// <param name="dto">Title, photo, call to action, hotspots, order and published flag.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The created look.</response>
    /// <response code="400">A field is invalid, or a hotspot names an unknown product ("invalid_hotspot_product").</response>
    [HttpPost("lookbook-looks")]
    [Consumes("application/json")]
    [ProducesResponseType<LookbookLookDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLookAsync([FromBody] SaveLookbookLookRequestDto dto, CancellationToken cancellationToken)
    {
        var created = await contentAppService.CreateLookAsync(dto, cancellationToken);
        return CreatedAtRoute("GetAdminLookbookLook", new { id = created.Id }, created);
    }

    /// <summary>Updates a look.</summary>
    /// <param name="id">Look identifier.</param>
    /// <param name="dto">The new field values.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="400">A field is invalid, or a hotspot names an unknown product ("invalid_hotspot_product").</response>
    /// <response code="404">No look has this id.</response>
    [HttpPut("lookbook-looks/{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<LookbookLookDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLookAsync(
        [FromRoute] Guid id, [FromBody] SaveLookbookLookRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await contentAppService.UpdateLookAsync(id, dto, cancellationToken));

    /// <summary>Deletes a look.</summary>
    /// <param name="id">Look identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">The look was deleted.</response>
    /// <response code="404">No look has this id.</response>
    [HttpDelete("lookbook-looks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLookAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await contentAppService.DeleteLookAsync(id, cancellationToken);
        return NoContent();
    }

    // ---------- FAQ ----------

    /// <summary>Returns every FAQ item, unpublished ones included, in page order.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    [HttpGet("faq-items")]
    [ProducesResponseType<IReadOnlyList<FaqItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFaqItemsAsync(CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetFaqItemsAsync(cancellationToken));

    /// <summary>Returns one FAQ item.</summary>
    /// <param name="id">FAQ item identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="404">No FAQ item has this id.</response>
    [HttpGet("faq-items/{id:guid}", Name = "GetAdminFaqItem")]
    [ProducesResponseType<FaqItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFaqItemAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await contentAppService.GetFaqItemAsync(id, cancellationToken));

    /// <summary>Creates a FAQ item.</summary>
    /// <param name="dto">Question, answer, order and published flag.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The created item.</response>
    /// <response code="400">A field is missing or too long ("invalid_content").</response>
    [HttpPost("faq-items")]
    [Consumes("application/json")]
    [ProducesResponseType<FaqItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFaqItemAsync([FromBody] SaveFaqItemRequestDto dto, CancellationToken cancellationToken)
    {
        var created = await contentAppService.CreateFaqItemAsync(dto, cancellationToken);
        return CreatedAtRoute("GetAdminFaqItem", new { id = created.Id }, created);
    }

    /// <summary>Updates a FAQ item.</summary>
    /// <param name="id">FAQ item identifier.</param>
    /// <param name="dto">The new field values.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="400">A field is missing or too long ("invalid_content").</response>
    /// <response code="404">No FAQ item has this id.</response>
    [HttpPut("faq-items/{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<FaqItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFaqItemAsync(
        [FromRoute] Guid id, [FromBody] SaveFaqItemRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await contentAppService.UpdateFaqItemAsync(id, dto, cancellationToken));

    /// <summary>Deletes a FAQ item.</summary>
    /// <param name="id">FAQ item identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">The item was deleted.</response>
    /// <response code="404">No FAQ item has this id.</response>
    [HttpDelete("faq-items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFaqItemAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await contentAppService.DeleteFaqItemAsync(id, cancellationToken);
        return NoContent();
    }
}
