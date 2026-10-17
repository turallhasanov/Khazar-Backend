namespace Khazar.Application.DTOs.Products;

/// <summary>
/// Catalog card payload. Mirrors the Product shape in Khazar-Frontend/src/data/mockProducts.js
/// (catalog, wishlist and lookbook components read fabric, stock and sizes, so they are included here).
/// </summary>
public record ProductListDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal BasePrice { get; init; }

    /// <summary>Pre-discount price; null when the product is not on sale.</summary>
    public decimal? CompareAtPrice { get; init; }

    public string Currency { get; init; } = string.Empty;
    public IReadOnlyList<ProductColorDto> Colors { get; init; } = [];
    public IReadOnlyList<string> Sizes { get; init; } = [];
    public IReadOnlyList<string> Images { get; init; } = [];
    public ProductFabricDto Fabric { get; init; } = new();

    /// <summary>Stock per size, summed across colors (e.g. { "M": 20 }).</summary>
    public IReadOnlyDictionary<string, int> Stock { get; init; } = new Dictionary<string, int>();

    public bool? IsNew { get; init; }
    public bool? IsBestseller { get; init; }
}
