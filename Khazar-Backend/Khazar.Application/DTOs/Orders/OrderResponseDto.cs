namespace Khazar.Application.DTOs.Orders;

/// <summary>Order confirmation payload; covers the fields the checkout success screen displays.</summary>
public sealed record OrderResponseDto
{
    public Guid Id { get; init; }

    /// <summary>Public order number in the KHZ-XXXXX format.</summary>
    public string OrderNumber { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];
}
