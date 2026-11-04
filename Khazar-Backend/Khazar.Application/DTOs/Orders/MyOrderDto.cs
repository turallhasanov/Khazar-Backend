namespace Khazar.Application.DTOs.Orders;

/// <summary>An order as the signed-in customer sees it in "My orders": status, total, date, address and photographed lines.</summary>
public sealed record MyOrderDto
{
    public Guid Id { get; init; }

    /// <summary>Public order number in the KHZ-XXXXX format.</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>Pending, Processing, SyncedToErp, Completed or Cancelled.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public string ShippingAddress { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyList<MyOrderItemDto> Items { get; init; } = [];
}

/// <summary>An order line with the product's cover photo (null when the product is gone).</summary>
public sealed record MyOrderItemDto
{
    public Guid ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal { get; init; }
    public string? ImageUrl { get; init; }
}
