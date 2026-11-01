using Khazar.Application.DTOs.Products;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Catalog management for platform admins (accounts listed in "Admin:Emails"). Errors carry an "errorCode" naming
/// the field or rule: invalid_name, invalid_slug, slug_taken, invalid_price, invalid_cover_image, invalid_images, invalid_image_color, variants_required,
/// invalid_sku, sku_taken, variant_exists, invalid_stock, stock_changed, product_has_orders, variant_has_orders,
/// last_variant.
/// </summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin/products")]
public sealed class AdminProductsController(IAdminProductAppService productAppService) : BaseApiController
{
    private const string GetProductRouteName = "GetAdminProduct";

    /// <summary>Returns every product, archived ones included, by name.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">All products with variants, stock, review count and order usage.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProductsAsync(CancellationToken cancellationToken) =>
        Ok(await productAppService.GetAllAsync(cancellationToken));

    /// <summary>Returns one product.</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The product.</response>
    /// <response code="404">No product has this id.</response>
    [HttpGet("{id:guid}", Name = GetProductRouteName)]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await productAppService.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a product with at least one variant.</summary>
    /// <param name="dto">Product fields and the initial variants.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The created product.</response>
    /// <response code="400">A field is invalid (see errorCode).</response>
    /// <response code="409">The slug or a SKU is already used ("slug_taken", "sku_taken", "variant_exists").</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProductAsync([FromBody] CreateProductRequestDto dto, CancellationToken cancellationToken)
    {
        var created = await productAppService.CreateAsync(dto, cancellationToken);
        return CreatedAtRoute(GetProductRouteName, new { id = created.Id }, created);
    }

    /// <summary>Updates the editable product fields. Variants are managed through the variant endpoints.</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="dto">The new field values.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The updated product.</response>
    /// <response code="400">A field is invalid (see errorCode).</response>
    /// <response code="404">No product has this id.</response>
    /// <response code="409">The slug is already used ("slug_taken").</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProductAsync(
        [FromRoute] Guid id, [FromBody] SaveProductRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await productAppService.UpdateAsync(id, dto, cancellationToken));

    /// <summary>Archives a product: it leaves the storefront, its orders, reviews and SKUs are kept.</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The archived product.</response>
    /// <response code="404">No product has this id.</response>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveProductAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await productAppService.ArchiveAsync(id, cancellationToken));

    /// <summary>Puts an archived product back on the storefront.</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The restored product.</response>
    /// <response code="404">No product has this id.</response>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreProductAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await productAppService.RestoreAsync(id, cancellationToken));

    /// <summary>Deletes a product that was never ordered, together with its variants and reviews.</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">The product was deleted.</response>
    /// <response code="404">No product has this id.</response>
    /// <response code="409">The product appears in orders ("product_has_orders"); archive it instead.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteProductAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await productAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Adds a variant (size, color, SKU, stock).</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="dto">The new variant.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The product with the new variant.</response>
    /// <response code="400">A field is invalid (see errorCode).</response>
    /// <response code="404">No product has this id.</response>
    /// <response code="409">The SKU or the color/size pair is already used ("sku_taken", "variant_exists").</response>
    [HttpPost("{id:guid}/variants")]
    [Consumes("application/json")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddVariantAsync(
        [FromRoute] Guid id, [FromBody] CreateVariantRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await productAppService.AddVariantAsync(id, dto, cancellationToken));

    /// <summary>Sets a variant's stock.</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="variantId">Variant identifier.</param>
    /// <param name="dto">The new stock quantity.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The updated product.</response>
    /// <response code="400">The quantity is out of range ("invalid_stock").</response>
    /// <response code="404">No such product or variant.</response>
    /// <response code="409">An order changed the stock meanwhile ("stock_changed"); reload and retry.</response>
    [HttpPatch("{id:guid}/variants/{variantId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateVariantStockAsync(
        [FromRoute] Guid id, [FromRoute] Guid variantId, [FromBody] UpdateVariantStockRequestDto dto,
        CancellationToken cancellationToken) =>
        Ok(await productAppService.UpdateVariantStockAsync(id, variantId, dto, cancellationToken));

    /// <summary>Removes a variant that was never ordered.</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="variantId">Variant identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The product without the variant.</response>
    /// <response code="404">No such product or variant.</response>
    /// <response code="409">The variant appears in orders, or is the last one ("variant_has_orders", "last_variant").</response>
    [HttpDelete("{id:guid}/variants/{variantId:guid}")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveVariantAsync(
        [FromRoute] Guid id, [FromRoute] Guid variantId, CancellationToken cancellationToken) =>
        Ok(await productAppService.RemoveVariantAsync(id, variantId, cancellationToken));
}
