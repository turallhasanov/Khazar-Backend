using Khazar.Domain.Common;
using Khazar.Domain.Exceptions;

namespace Khazar.Domain.Entities;

public class Product : BaseEntity
{
    private readonly List<ProductVariant> _variants = [];

    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal Price { get; private set; }
    public string Fabric { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public string CoverImage { get; private set; } = default!;
    public bool IsFeatured { get; private set; }

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    // Required by ORMs
    private Product() { }

    public Product(
        string name,
        string slug,
        string description,
        decimal price,
        string fabric,
        string category,
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
        string category,
        string coverImage)
    {
        SetDetails(name, slug, description, fabric, category, coverImage);
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

    public ProductVariant AddVariant(string size, string color, string sku, int stockQuantity)
    {
        var normalizedSku = Guard.NotEmpty(sku, nameof(ProductVariant.SKU)).ToUpperInvariant();
        if (_variants.Any(v => v.SKU == normalizedSku))
            throw new DomainException($"SKU '{normalizedSku}' already exists on this product.");

        var variant = new ProductVariant(Id, size, color, normalizedSku, stockQuantity, _variants.Count);
        _variants.Add(variant);
        MarkUpdated();
        return variant;
    }

    private void SetDetails(
        string name,
        string slug,
        string description,
        string fabric,
        string category,
        string coverImage)
    {
        Name = Guard.NotEmpty(name, nameof(Name));
        Slug = Guard.NotEmpty(slug, nameof(Slug)).ToLowerInvariant();
        Description = description?.Trim() ?? string.Empty;
        Fabric = Guard.NotEmpty(fabric, nameof(Fabric));
        Category = Guard.NotEmpty(category, nameof(Category));
        CoverImage = Guard.NotEmpty(coverImage, nameof(CoverImage));
    }
}
