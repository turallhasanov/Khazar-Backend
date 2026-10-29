using Khazar.Application.DTOs.Categories;
using Khazar.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>Read-only storefront category endpoint.</summary>
public sealed class CategoriesController(ICategoryAppService categoryAppService) : BaseApiController
{
    /// <summary>Returns every category in storefront order (SortOrder, then Name).</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">All categories, including ones without active products.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategoriesAsync(CancellationToken cancellationToken) =>
        Ok(await categoryAppService.GetAllAsync(cancellationToken));
}
