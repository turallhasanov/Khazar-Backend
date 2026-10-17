using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; private set; }
    public string Size { get; private set; } = default!;
    public string Color { get; private set; } = default!;
    public string SKU { get; private set; } = default!;
    public int StockQuantity { get; private set; }

    public Product? Product { get; private set; }

    public bool IsInStock => StockQuantity > 0;

    // Required by ORMs
    private ProductVariant() { }

    internal ProductVariant(Guid productId, string size, string color, string sku, int stockQuantity)
    {
        ProductId = Guard.NotEmpty(productId, nameof(ProductId));
        Size = Guard.NotEmpty(size, nameof(Size));
        Color = Guard.NotEmpty(color, nameof(Color));
        SKU = Guard.NotEmpty(sku, nameof(SKU)).ToUpperInvariant();
        StockQuantity = Guard.NotNegative(stockQuantity, nameof(StockQuantity));
    }

    public void SetStock(int quantity)
    {
        StockQuantity = Guard.NotNegative(quantity, nameof(StockQuantity));
        MarkUpdated();
    }

    public void IncreaseStock(int quantity)
    {
        StockQuantity += Guard.Positive(quantity, nameof(quantity));
        MarkUpdated();
    }

    public void DecreaseStock(int quantity)
    {
        Guard.Positive(quantity, nameof(quantity));
        if (quantity > StockQuantity)
            throw new DomainException(
                $"Insufficient stock for '{SKU}'. Requested: {quantity}, available: {StockQuantity}.");

        StockQuantity -= quantity;
        MarkUpdated();
    }
}
