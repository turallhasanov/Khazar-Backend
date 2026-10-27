namespace Khazar.Application.DTOs.Products;

public sealed record UpdateVariantStockRequestDto
{
    public int StockQuantity { get; init; }
}
