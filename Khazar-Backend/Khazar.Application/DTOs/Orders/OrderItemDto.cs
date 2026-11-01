namespace Khazar.Application.DTOs.Orders;

/// <summary>An order line as stored: a snapshot of the product at the time of ordering.</summary>
public sealed record OrderItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public Guid VariantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal { get; init; }
}
