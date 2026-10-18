using Khazar.Application.DTOs.Products;
using Khazar.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>Read-only storefront catalog endpoints.</summary>
public sealed class ProductsController(IProductAppService productAppService) : BaseApiController
{
    /// <summary>Returns the full catalog ordered by name.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductListDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var products = await productAppService.GetAllAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>Returns a single product with its variants.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await productAppService.GetByIdAsync(id, cancellationToken);
        return product is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Product '{id}' was not found.")
            : Ok(product);
    }

    /// <summary>Returns a single product by its URL slug (case-insensitive), as used by the product detail page.</summary>
    [HttpGet("slug/{slug}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var product = await productAppService.GetBySlugAsync(slug, cancellationToken);
        return product is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Product with slug '{slug}' was not found.")
            : Ok(product);
    }

    /// <summary>Returns the products in a category. An unknown category returns an empty list, not 404.</summary>
    [HttpGet("category/{category}")]
    [ProducesResponseType<IReadOnlyList<ProductListDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCategoryAsync(string category, CancellationToken cancellationToken)
    {
        var products = await productAppService.GetByCategoryAsync(category, cancellationToken);
        return Ok(products);
    }
}
