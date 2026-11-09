using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

/// <summary>
/// Product persistence contract. GetAllAsync and GetByIdAsync are inherited from <see cref="IAsyncRepository{T}"/>.
/// The storefront reads (GetAllAsync, GetBySlugAsync, GetByCategoryAsync) return active products only; GetByIdAsync
/// returns archived ones too, so callers check <see cref="Product.IsActive"/> themselves.
/// AddAsync and UpdateAsync report a duplicate slug or SKU as a <see cref="Exceptions.ConflictException"/>, and a
/// stock change that raced with a checkout as one with the code "stock_changed".
/// </summary>
public interface IProductRepository : IAsyncRepository<Product>
{
    /// <summary>Finds a product by its URL slug. Slugs are stored lower-case, so lookups are case-insensitive.</summary>
    Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>Every product, archived ones included, with the review and order facts the admin panel needs.</summary>
    Task<IReadOnlyList<ManagedProduct>> GetAllForAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>One product (tracked, for changes) with its review and order facts, or null.</summary>
    Task<ManagedProduct?> GetForAdminByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked products (archived included) that have a variant with one of the upper-case SKUs.</summary>
    Task<IReadOnlyList<Product>> GetByVariantSkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default);

    /// <summary>
    /// The oldest active product with exactly this name (case-insensitive), tracked and with its variants, or null.
    /// NexusERP variants named "Name (Size)" are gathered under that one product.
    /// </summary>
    Task<Product?> GetActiveByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, Guid? exceptProductId = null, CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default);
}

/// <summary>
/// A product for the admin panel. A product that appears in any order (<paramref name="HasOrders"/>) can be archived
/// but not deleted; <paramref name="OrderedVariantIds"/> are the variants that cannot be removed for the same reason.
/// </summary>
public sealed record ManagedProduct(
    Product Product, int ReviewCount, bool HasOrders, IReadOnlySet<Guid> OrderedVariantIds);
