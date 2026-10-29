namespace Khazar.Application.DTOs.Products;

/// <summary>A product as the admin panel manages it, archived ones included.</summary>
public sealed record AdminProductDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }

    /// <summary>The category's Name, the same value the storefront API returns as "category".</summary>
    public string Category { get; init; } = string.Empty;

    public string CategoryLabel { get; init; } = string.Empty;
    public string Fabric { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string CoverImage { get; init; } = string.Empty;
    public bool IsFeatured { get; init; }

    /// <summary>False when archived: hidden from the storefront and not orderable.</summary>
    public bool IsActive { get; init; }

    public int TotalStock { get; init; }
    public int ReviewCount { get; init; }

    /// <summary>True when the product appears in an order; it can then be archived but not deleted.</summary>
    public bool HasOrders { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<AdminProductVariantDto> Variants { get; init; } = [];
}
