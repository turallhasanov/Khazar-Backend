namespace Khazar.Application.DTOs.Products;

/// <summary>Editable product fields, for both create and update.</summary>
public record SaveProductRequestDto
{
    public string Name { get; init; } = string.Empty;

    /// <summary>URL segment: lower-case letters, digits and single hyphens, e.g. "heavyweight-hoodie".</summary>
    public string Slug { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Fabric { get; init; } = string.Empty;

    /// <summary>Price in AZN, at most two decimals.</summary>
    public decimal Price { get; init; }

    /// <summary>Absolute http(s) image URL.</summary>
    public string CoverImage { get; init; } = string.Empty;

    public bool IsFeatured { get; init; }
}
