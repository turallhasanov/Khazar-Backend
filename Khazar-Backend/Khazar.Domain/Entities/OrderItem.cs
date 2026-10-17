using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>
/// A snapshot of the product data at the time of ordering.
/// Order history is preserved even if the product name or price changes later.
/// </summary>
public class OrderItem : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid VariantId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public string Size { get; private set; } = default!;
    public string Color { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public Order? Order { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    // Required by ORMs
    private OrderItem() { }

    internal OrderItem(
        Guid orderId,
        Guid productId,
        Guid variantId,
        string productName,
        string size,
        string color,
        decimal unitPrice,
        int quantity)
    {
        OrderId = Guard.NotEmpty(orderId, nameof(OrderId));
        ProductId = Guard.NotEmpty(productId, nameof(ProductId));
        VariantId = Guard.NotEmpty(variantId, nameof(VariantId));
        ProductName = Guard.NotEmpty(productName, nameof(ProductName));
        Size = Guard.NotEmpty(size, nameof(Size));
        Color = Guard.NotEmpty(color, nameof(Color));
        UnitPrice = Guard.NotNegative(unitPrice, nameof(UnitPrice));
        Quantity = Guard.Positive(quantity, nameof(Quantity));
    }

    internal void IncreaseQuantity(int quantity)
    {
        Quantity += Guard.Positive(quantity, nameof(quantity));
        MarkUpdated();
    }
}
