using Khazar.Domain.Common;
using Khazar.Domain.Exceptions;

namespace Khazar.Domain.Entities;

public class Product : BaseEntity
{
    public const int MaxNameLength = 200;
    public const int MaxSlugLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxFabricLength = 200;
    public const int MaxCoverImageLength = 1000;

    private readonly List<ProductVariant> _variants = [];

    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal Price { get; private set; }
    public string Fabric { get; private set; } = default!;
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = default!;
    public string CoverImage { get; private set; } = default!;

    /// <summary>Optional second photo (another angle or worn on a model) the storefront card fades to on hover.</summary>
    public string? HoverImage { get; private set; }
    public bool IsFeatured { get; private set; }

    /// <summary>
    /// False once archived: the product leaves the storefront and cannot be ordered, but its order history,
    /// reviews and NexusERP SKUs stay intact. Products that were ordered are archived rather than deleted.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    // Required by ORMs
    private Product() { }

    public Product(
        string name,
        string slug,
        string description,
        decimal price,
        string fabric,
        Category category,
        string coverImage,
        bool isFeatured = false)
    {
        SetDetails(name, slug, description, fabric, category, coverImage);
        Price = Guard.NotNegative(price, nameof(Price));
        IsFeatured = isFeatured;
    }

    public void UpdateDetails(
        string name,
        string slug,
        string description,
        string fabric,
        Category category,
        string coverImage)
    {
        SetDetails(name, slug, description, fabric, category, coverImage);
        MarkUpdated();
    }

    /// <summary>Sets the hover photo; null or whitespace removes it.</summary>
    public void SetHoverImage(string? hoverImage)
    {
        HoverImage = string.IsNullOrWhiteSpace(hoverImage) ? null : hoverImage.Trim();
        MarkUpdated();
    }

    public void ChangePrice(decimal price)
    {
        Price = Guard.NotNegative(price, nameof(Price));
        MarkUpdated();
    }

    public void SetFeatured(bool isFeatured)
    {
        IsFeatured = isFeatured;
        MarkUpdated();
    }

    public void Archive()
    {
        if (!IsActive)
            return;

        IsActive = false;
        MarkUpdated();
    }

    public void Restore()
    {
        if (IsActive)
            return;

        IsActive = true;
        MarkUpdated();
    }

    public ProductVariant AddVariant(string size, string color, string sku, int stockQuantity)
    {
        var normalizedSku = Guard.NotEmpty(sku, nameof(ProductVariant.SKU)).ToUpperInvariant();
        if (_variants.Any(v => v.SKU == normalizedSku))
            throw new DomainException($"SKU '{normalizedSku}' already exists on this product.");

        // Checkout resolves a variant by color and size, so the pair must stay unique.
        if (_variants.Any(v => v.Color.Equals(color?.Trim(), StringComparison.OrdinalIgnoreCase)
                               && v.Size.Equals(size?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"A variant with color '{color}' and size '{size}' already exists on this product.");

        var sortOrder = _variants.Count == 0 ? 0 : _variants.Max(v => v.SortOrder) + 1;
        // Empty values are rejected by the ProductVariant constructor.
        var variant = new ProductVariant(
            Id, size?.Trim() ?? string.Empty, color?.Trim() ?? string.Empty, normalizedSku, stockQuantity, sortOrder);
        _variants.Add(variant);
        MarkUpdated();
        return variant;
    }

    /// <summary>Removes a variant. Callers must first make sure no order references it.</summary>
    public void RemoveVariant(Guid variantId)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new DomainException($"Variant '{variantId}' does not belong to this product.");

        _variants.Remove(variant);
        MarkUpdated();
    }

    private void SetDetails(
        string name,
        string slug,
        string description,
        string fabric,
        Category category,
        string coverImage)
    {
        ArgumentNullException.ThrowIfNull(category);
        Name = Guard.NotEmpty(name, nameof(Name));
        Slug = Guard.NotEmpty(slug, nameof(Slug)).ToLowerInvariant();
        Description = description?.Trim() ?? string.Empty;
        Fabric = Guard.NotEmpty(fabric, nameof(Fabric));
        Category = category;
        CategoryId = category.Id;
        CoverImage = Guard.NotEmpty(coverImage, nameof(CoverImage));
    }
}
