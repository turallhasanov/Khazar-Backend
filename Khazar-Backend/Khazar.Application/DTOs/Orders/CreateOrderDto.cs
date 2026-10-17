namespace Khazar.Application.DTOs.Orders;

/// <summary>
/// Checkout request. Mirrors the orderPayload built in Khazar-Frontend/src/pages/CheckoutPage.jsx.
/// Client-computed values in that payload (orderNumber, createdAt, unit prices, line totals, totals)
/// are intentionally not bound: the server generates the number and recalculates every amount.
/// </summary>
public sealed record CreateOrderDto
{
    /// <summary>Sales channel, e.g. "khazar-web".</summary>
    public string Source { get; init; } = string.Empty;

    public CustomerDto Customer { get; init; } = new();
    public ShippingAddressDto Shipping { get; init; } = new();
    public PaymentDto Payment { get; init; } = new();
    public IReadOnlyList<CreateOrderItemDto> Items { get; init; } = [];
}
