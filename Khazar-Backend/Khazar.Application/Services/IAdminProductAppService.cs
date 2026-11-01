using Khazar.Application.DTOs.Products;

namespace Khazar.Application.Services;

/// <summary>
/// Catalog management for platform admins. Failures carry an errorCode naming the field or rule, e.g. "invalid_slug",
/// "slug_taken", "invalid_category", "sku_taken", "product_has_orders", so the admin panel can point at the right input.
/// </summary>
public interface IAdminProductAppService
{
    /// <summary>Every product, archived ones included, by name.</summary>
    Task<IReadOnlyList<AdminProductDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<AdminProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdminProductDto> CreateAsync(CreateProductRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Replaces the editable fields (name, slug, description, category, fabric, price, image, featured).</summary>
    Task<AdminProductDto> UpdateAsync(Guid id, SaveProductRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Hides the product from the storefront; orders, reviews and SKUs are kept.</summary>
    Task<AdminProductDto> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdminProductDto> RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a product that was never ordered, with its variants and reviews ("product_has_orders" otherwise).</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdminProductDto> AddVariantAsync(Guid productId, CreateVariantRequestDto dto, CancellationToken cancellationToken = default);

    Task<AdminProductDto> UpdateVariantStockAsync(
        Guid productId, Guid variantId, UpdateVariantStockRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Removes a variant that was never ordered ("variant_has_orders" otherwise).</summary>
    Task<AdminProductDto> RemoveVariantAsync(Guid productId, Guid variantId, CancellationToken cancellationToken = default);
}
