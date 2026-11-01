using Khazar.Application.DTOs.Products;
using Khazar.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>Read-only storefront catalog endpoints.</summary>
public sealed class ProductsController(IProductAppService productAppService) : BaseApiController
{
    /// <summary>Returns the catalog ordered by name, optionally filtered by category.</summary>
    /// <param name="category">Category to filter by (case-insensitive). Omit it to get the full catalog.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The matching products. An unknown category returns an empty list, not 404.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductListDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductsAsync([FromQuery] string? category, CancellationToken cancellationToken)
    {
        var products = string.IsNullOrWhiteSpace(category)
            ? await productAppService.GetAllAsync(cancellationToken)
            : await productAppService.GetByCategoryAsync(category, cancellationToken);
        return Ok(products);
    }

    /// <summary>Returns a single product with its variants.</summary>
    /// <param name="id">Product identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The product.</response>
    /// <response code="404">No product has this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductByIdAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var product = await productAppService.GetByIdAsync(id, cancellationToken);
        return product is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Product '{id}' was not found.")
            : Ok(product);
    }

    /// <summary>Returns a single product by its URL slug, as used by the product detail page.</summary>
    /// <param name="slug">URL slug of the product (case-insensitive).</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The product.</response>
    /// <response code="404">No product has this slug.</response>
    [HttpGet("slug/{slug}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductBySlugAsync([FromRoute] string slug, CancellationToken cancellationToken)
    {
        var product = await productAppService.GetBySlugAsync(slug, cancellationToken);
        return product is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Product with slug '{slug}' was not found.")
            : Ok(product);
    }
}
