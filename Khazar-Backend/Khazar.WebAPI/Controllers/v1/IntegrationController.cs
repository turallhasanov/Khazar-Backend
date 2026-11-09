using System.Security.Cryptography;
using System.Text;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Orders;
using Khazar.Application.Services;
using Khazar.WebAPI.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Server-to-server endpoints for NexusERP, protected by the shared secret "Integration:ApiKey" (header X-Api-Key)
/// instead of a user login. Answers 404 while no key is configured, so the endpoint does not exist by default.
/// </summary>
[AllowAnonymous]
[Route("api/integration")]
public sealed class IntegrationController(
    IProductRepository productRepository,
    IAdminOrderAppService orderAppService,
    INexusProductSyncService productSyncService,
    ImageFileStore imageStore,
    IConfiguration configuration) : BaseApiController
{
    /// <summary>
    /// One row per product variant: SKU (NexusERP barcode), name, category, size, color, price, stock and photo (a site
    /// path such as /images/x.jpg or /uploads/products/x.png, relative to this API).
    /// </summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">Every variant, archived products flagged with isActive = false.</response>
    /// <response code="401">The X-Api-Key header is missing or wrong.</response>
    /// <response code="404">No Integration:ApiKey is configured.</response>
    [HttpGet("catalog")]
    [ProducesResponseType<IReadOnlyList<IntegrationCatalogItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (CheckApiKey() is { } rejected)
            return rejected;

        var products = await productRepository.GetAllForAdminAsync(cancellationToken);

        return Ok(products.SelectMany(managed => managed.Product.Variants.Select(variant => new IntegrationCatalogItemDto(
            variant.SKU,
            managed.Product.Name,
            managed.Product.Category.Name,
            variant.Size,
            variant.Color,
            managed.Product.Price,
            variant.StockQuantity,
            managed.Product.IsActive,
            // The photo of the variant's color when the gallery has one, otherwise the cover.
            managed.Product.Images
                .FirstOrDefault(image => image.Color is not null && image.Color.Equals(variant.Color, StringComparison.OrdinalIgnoreCase))
                ?.Url ?? managed.Product.CoverImage))).ToList());
    }

    /// <summary>
    /// NexusERP cancelled the sale of this order (an admin deleted it there): cancels the order here and returns its items
    /// to stock. Khazar does not call NexusERP back. Repeating the call for a cancelled order succeeds.
    /// </summary>
    /// <param name="orderNumber">Public order number, e.g. KHZ-7Q2MX.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The cancelled order.</response>
    /// <response code="401">The X-Api-Key header is missing or wrong.</response>
    /// <response code="404">No Integration:ApiKey is configured, or no order has this number ("not_found").</response>
    /// <response code="409">The order is already completed ("invalid_order_status").</response>
    [HttpDelete("orders/{orderNumber}")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelOrderAsync([FromRoute] string orderNumber, CancellationToken cancellationToken)
    {
        if (CheckApiKey() is { } rejected)
            return rejected;

        return Ok(await orderAppService.CancelFromErpAsync(orderNumber, cancellationToken));
    }

    /// <summary>
    /// NexusERP saved a product: adds it to the catalog, or updates it (stock, price, photo, shown or hidden). The photo, if
    /// any, is stored under wwwroot/uploads/products and becomes the product's first image.
    /// </summary>
    /// <param name="form">sku (the NexusERP barcode), name, categoryName, salePrice, stock, isPublished and an optional image file.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">{ "outcome": "created" | "updated" | "unchanged" | "skipped", "slug", "imageUrl" }</response>
    /// <response code="400">Invalid fields ("invalid_sku", "invalid_name", "invalid_price") or image ("invalid_image", "image_too_large").</response>
    /// <response code="401">The X-Api-Key header is missing or wrong.</response>
    /// <response code="404">No Integration:ApiKey is configured.</response>
    [HttpPost("products")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageFileStore.MaxFileBytes * (1 + IntegrationProductForm.MaxGalleryImages) + 64 * 1024)]
    [ProducesResponseType<NexusProductPushResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PushProductAsync([FromForm] IntegrationProductForm form, CancellationToken cancellationToken)
    {
        if (CheckApiKey() is { } rejected)
            return rejected;

        var galleryFiles = form.GalleryImages?.Where(file => file.Length > 0).ToList() ?? [];
        if (galleryFiles.Count > IntegrationProductForm.MaxGalleryImages)
            throw new Khazar.Application.Common.Exceptions.ValidationException(
                $"At most {IntegrationProductForm.MaxGalleryImages} extra photos can be sent with a product.", "too_many_images");

        string? imageUrl = null;
        var galleryUrls = new List<string>();

        try
        {
            if (form.Image is { Length: > 0 })
                imageUrl = await imageStore.SaveAsync(form.Image, ImageFileStore.ProductUploadsFolder, cancellationToken);

            foreach (var file in galleryFiles)
                galleryUrls.Add(await imageStore.SaveAsync(file, ImageFileStore.ProductUploadsFolder, cancellationToken));

            var result = await productSyncService.PushProductAsync(
                new NexusProductPush(form.Sku, form.Name, form.CategoryName, form.SalePrice, form.Stock, form.IsPublished, imageUrl)
                {
                    GalleryUrls = galleryUrls,
                },
                cancellationToken);

            // The product no longer shows the photos the new one replaced.
            foreach (var replaced in result.ReplacedImageUrls)
                imageStore.TryDelete(replaced);

            return Ok(result);
        }
        catch
        {
            // The product was refused, so the photos that came with it would only be left behind.
            if (imageUrl is not null)
                imageStore.TryDelete(imageUrl);
            foreach (var url in galleryUrls)
                imageStore.TryDelete(url);
            throw;
        }
    }

    /// <summary>Null when the request carries the configured shared secret; otherwise the response to return.</summary>
    private IActionResult? CheckApiKey()
    {
        var expected = configuration["Integration:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
            return NotFound();

        var provided = Request.Headers["X-Api-Key"].ToString();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected))
            ? null
            : Unauthorized();
    }
}

/// <summary>Body of POST api/integration/products (multipart/form-data).</summary>
public sealed class IntegrationProductForm
{
    /// <summary>Photos beyond the main one; the gallery holds at most 12 in all.</summary>
    public const int MaxGalleryImages = 7;

    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? CategoryName { get; init; }
    public decimal SalePrice { get; init; }
    public int Stock { get; init; }
    public bool IsPublished { get; init; }
    public IFormFile? Image { get; init; }

    /// <summary>Further photos, in display order after <see cref="Image"/>.</summary>
    public IFormFileCollection? GalleryImages { get; init; }
}

public sealed record IntegrationCatalogItemDto(
    string Sku, string Name, string Category, string Size, string Color, decimal Price, int Stock, bool IsActive, string? ImageUrl);
