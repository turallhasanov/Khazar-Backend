namespace Khazar.Application.DTOs.Products;

public sealed record AdminProductVariantDto
{
    public Guid Id { get; init; }
    public string Size { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public int StockQuantity { get; init; }

    /// <summary>True when the variant appears in an order; it can then not be removed.</summary>
    public bool HasOrders { get; init; }
}
