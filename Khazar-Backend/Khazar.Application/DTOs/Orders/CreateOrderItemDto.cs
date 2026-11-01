namespace Khazar.Application.DTOs.Orders;

/// <summary>
/// A checkout line. The frontend has no variant ids yet (its variantId is a "productId::color::size"
/// cart key), so the variant is resolved on the server from <see cref="StockRef"/>.
/// </summary>
public sealed record CreateOrderItemDto
{
    public Guid ProductId { get; init; }
    public StockRefDto StockRef { get; init; } = new();
    public int Quantity { get; init; }
}
