using Khazar.Application.DTOs.Categories;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Category management for platform admins (accounts listed in "Admin:Emails"). Errors carry an "errorCode" naming
/// the field or rule: invalid_name, invalid_label, invalid_slug, invalid_sort_order, category_name_taken,
/// category_slug_taken, category_has_products.
/// </summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin/categories")]
public sealed class AdminCategoriesController(IAdminCategoryAppService categoryAppService) : BaseApiController
{
    private const string GetCategoryRouteName = "GetAdminCategory";

    /// <summary>Returns every category in storefront order, with its product count.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">All categories.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminCategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategoriesAsync(CancellationToken cancellationToken) =>
        Ok(await categoryAppService.GetAllAsync(cancellationToken));

    /// <summary>Returns one category.</summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The category.</response>
    /// <response code="404">No category has this id.</response>
    [HttpGet("{id:guid}", Name = GetCategoryRouteName)]
    [ProducesResponseType<AdminCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await categoryAppService.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a category.</summary>
    /// <param name="dto">Name, label, slug and sort order.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The created category.</response>
    /// <response code="400">A field is invalid (see errorCode).</response>
    /// <response code="409">The name or slug is already used ("category_name_taken", "category_slug_taken").</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<AdminCategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategoryAsync([FromBody] SaveCategoryRequestDto dto, CancellationToken cancellationToken)
    {
        var created = await categoryAppService.CreateAsync(dto, cancellationToken);
        return CreatedAtRoute(GetCategoryRouteName, new { id = created.Id }, created);
    }

    /// <summary>Updates a category. Its products follow it, so a rename applies to all of them.</summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="dto">The new field values.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The updated category.</response>
    /// <response code="400">A field is invalid (see errorCode).</response>
    /// <response code="404">No category has this id.</response>
    /// <response code="409">The name or slug is already used ("category_name_taken", "category_slug_taken").</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<AdminCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategoryAsync(
        [FromRoute] Guid id, [FromBody] SaveCategoryRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await categoryAppService.UpdateAsync(id, dto, cancellationToken));

    /// <summary>Deletes a category that has no products.</summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">The category was deleted.</response>
    /// <response code="404">No category has this id.</response>
    /// <response code="409">The category still has products ("category_has_products"); move them first.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategoryAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await categoryAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
