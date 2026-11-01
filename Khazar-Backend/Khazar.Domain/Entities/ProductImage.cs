using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>
/// One photo of a product's gallery. The first photo is the catalog cover, the second the card's hover photo.
/// <see cref="Color"/> ties the photo to a variant color, so the product page shows it when that color is chosen.
/// </summary>
public class ProductImage : BaseEntity
{
    public const int MaxUrlLength = 1000;

    public Guid ProductId { get; private set; }
    public string Url { get; private set; } = default!;

    /// <summary>A variant color name ("Antrasit"), or null for a photo that shows no particular color.</summary>
    public string? Color { get; private set; }

    public int SortOrder { get; private set; }

    // Required by ORMs
    private ProductImage() { }

    internal ProductImage(Guid productId, string url, string? color, int sortOrder)
    {
        ProductId = productId;
        Url = Guard.MaxLength(Guard.NotEmpty(url, nameof(Url)), MaxUrlLength, nameof(Url));
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        SortOrder = Guard.NotNegative(sortOrder, nameof(SortOrder));
    }
}
