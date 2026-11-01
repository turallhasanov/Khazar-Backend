namespace Khazar.Application.DTOs.Products;

public sealed record ProductVariantDto
{
    public Guid Id { get; init; }
    public string Size { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public int StockQuantity { get; init; }
    public bool IsInStock { get; init; }
}
