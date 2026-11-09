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

    public const int MaxImages = 12;

    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductImage> _images = [];

    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal Price { get; private set; }
    public string Fabric { get; private set; } = default!;
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = default!;
    /// <summary>The gallery's first photo (kept in sync by <see cref="ReplaceImages"/>), used by catalog cards and emails.</summary>
    public string CoverImage { get; private set; } = default!;

    /// <summary>The gallery's second photo, which the storefront card fades to on hover; null with a single photo.</summary>
    public string? HoverImage { get; private set; }
    public bool IsFeatured { get; private set; }

    /// <summary>
    /// False once archived: the product leaves the storefront and cannot be ordered, but its order history,
    /// reviews and NexusERP SKUs stay intact. Products that were ordered are archived rather than deleted.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    /// <summary>The gallery in display order; never empty.</summary>
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

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
        SetDetails(name, slug, description, fabric, category);
        ReplaceImages([(coverImage, null)]);
        Price = Guard.NotNegative(price, nameof(Price));
        IsFeatured = isFeatured;
    }

    public void UpdateDetails(
        string name,
        string slug,
        string description,
        string fabric,
        Category category)
    {
        SetDetails(name, slug, description, fabric, category);
        MarkUpdated();
    }

    /// <summary>
    /// Replaces the gallery (1 to <see cref="MaxImages"/> photos, in display order). <see cref="CoverImage"/> and
    /// <see cref="HoverImage"/> follow the first two photos. Colors are not checked here; callers match them to variants.
    /// </summary>
    public void ReplaceImages(IReadOnlyList<(string Url, string? Color)> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count is 0 or > MaxImages)
            throw new DomainException($"A product needs between 1 and {MaxImages} images.");

        _images.Clear();
        for (var i = 0; i < images.Count; i++)
            _images.Add(new ProductImage(Id, images[i].Url, images[i].Color, i));

        CoverImage = _images[0].Url;
        HoverImage = _images.Count > 1 ? _images[1].Url : null;
        MarkUpdated();
    }

    /// <summary>Sets the second (hover) photo, keeping the others; null or whitespace removes it.</summary>
    public void SetHoverImage(string? hoverImage)
    {
        var images = _images.OrderBy(i => i.SortOrder).Select(i => (i.Url, i.Color)).ToList();
        if (images.Count > 1)
            images.RemoveAt(1);
        if (!string.IsNullOrWhiteSpace(hoverImage))
            images.Insert(1, (hoverImage.Trim(), null));
        ReplaceImages(images);
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

    /// <summary>Changes a variant's size and color; the pair must stay unique within the product.</summary>
    public void ChangeVariantAttributes(Guid variantId, string size, string color)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new DomainException("Variant not found.");

        if (_variants.Any(v => v.Id != variantId
                               && v.Color.Equals(color?.Trim(), StringComparison.OrdinalIgnoreCase)
                               && v.Size.Equals(size?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"A variant with color '{color}' and size '{size}' already exists on this product.");

        variant.ChangeAttributes(size?.Trim() ?? string.Empty, color?.Trim() ?? string.Empty);
        MarkUpdated();
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
        Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        Name = Guard.NotEmpty(name, nameof(Name));
        Slug = Guard.NotEmpty(slug, nameof(Slug)).ToLowerInvariant();
        Description = description?.Trim() ?? string.Empty;
        Fabric = Guard.NotEmpty(fabric, nameof(Fabric));
        Category = category;
        CategoryId = category.Id;
    }
}
