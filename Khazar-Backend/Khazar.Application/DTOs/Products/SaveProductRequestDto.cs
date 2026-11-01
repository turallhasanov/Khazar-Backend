namespace Khazar.Application.DTOs.Products;

/// <summary>Editable product fields, for both create and update.</summary>
public record SaveProductRequestDto
{
    public string Name { get; init; } = string.Empty;

    /// <summary>URL segment: lower-case letters, digits and single hyphens, e.g. "heavyweight-hoodie".</summary>
    public string Slug { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>Id of an existing category (GET /api/admin/categories).</summary>
    public Guid CategoryId { get; init; }

    public string Fabric { get; init; } = string.Empty;

    /// <summary>Price in AZN, at most two decimals.</summary>
    public decimal Price { get; init; }

    /// <summary>Site path of an uploaded image ("/images/abc.jpg"). Ignored when <see cref="Images"/> is sent (its first photo is the cover).</summary>
    public string CoverImage { get; init; } = string.Empty;

    /// <summary>
    /// The whole gallery in display order (1–12 photos), replacing the current one. Each color must be one of the
    /// product's variant colors. Omit to keep a single-photo gallery of <see cref="CoverImage"/> (older clients).
    /// </summary>
    public IReadOnlyList<ProductImageDto>? Images { get; init; }

    public bool IsFeatured { get; init; }
}
