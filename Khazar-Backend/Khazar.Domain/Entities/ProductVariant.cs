using Khazar.Domain.Common;
using Khazar.Domain.Exceptions;

namespace Khazar.Domain.Entities;

public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; private set; }
    public string Size { get; private set; } = default!;
    public string Color { get; private set; } = default!;
    public string SKU { get; private set; } = default!;
    public int StockQuantity { get; private set; }

    /// <summary>Display position within the product; preserves the merchandised color order.</summary>
    public int SortOrder { get; private set; }

    public Product? Product { get; private set; }

    public bool IsInStock => StockQuantity > 0;

    // Required by ORMs
    private ProductVariant() { }

    internal ProductVariant(Guid productId, string size, string color, string sku, int stockQuantity, int sortOrder)
    {
        ProductId = Guard.NotEmpty(productId, nameof(ProductId));
        Size = Guard.NotEmpty(size, nameof(Size));
        Color = Guard.NotEmpty(color, nameof(Color));
        SKU = Guard.NotEmpty(sku, nameof(SKU)).ToUpperInvariant();
        StockQuantity = Guard.NotNegative(stockQuantity, nameof(StockQuantity));
        SortOrder = Guard.NotNegative(sortOrder, nameof(SortOrder));
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
            throw new InsufficientStockException(SKU);

        StockQuantity -= quantity;
        MarkUpdated();
    }
}
