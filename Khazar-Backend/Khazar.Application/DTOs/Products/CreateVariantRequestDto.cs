namespace Khazar.Application.DTOs.Products;

public sealed record CreateVariantRequestDto
{
    public string Size { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;

    /// <summary>Stock keeping unit; NexusERP matches products by it, so it cannot be changed later.</summary>
    public string Sku { get; init; } = string.Empty;

    public int StockQuantity { get; init; }
}
