namespace Khazar.Application.DTOs.Orders;

/// <summary>Color and size pair that identifies a product variant.</summary>
public sealed record StockRefDto
{
    public string Color { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
}
