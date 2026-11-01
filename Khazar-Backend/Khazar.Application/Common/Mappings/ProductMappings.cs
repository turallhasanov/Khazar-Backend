using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Products;
using Khazar.Domain.Entities;
using Khazar.Domain.Enums;

namespace Khazar.Application.Common.Mappings;

/// <summary>Explicit Product to DTO mappings.</summary>
public static class ProductMappings
{
    // Prices are stored without a currency on Product; the storefront sells in AZN only.
    private static readonly string DefaultCurrency = Currency.AZN.ToString();

    // Apparel size order used by the frontend (XS…XXL). Unknown sizes sort after these.
    private static readonly string[] SizeOrder = ["XS", "S", "M", "L", "XL", "XXL"];

    public static ProductListDto ToListDto(this Product product) => new()
    {
        Id = product.Id,
        Slug = product.Slug,
        Name = product.Name,
        Category = product.Category.Name,
        CategoryLabel = product.Category.Label,
        BasePrice = product.Price,
        Currency = DefaultCurrency,
        Colors = MapColors(product),
        Sizes = MapSizes(product),
        Images = MapImages(product).Select(image => image.Url).ToList(),
        Gallery = MapImages(product),
        Fabric = new ProductFabricDto { Material = product.Fabric },
        Stock = MapStock(product),
        // The storefront's "Bestseller" label and "featured" sort read this flag.
        IsBestseller = product.IsFeatured,
    };

    public static ProductDetailDto ToDetailDto(this Product product) => new(product.ToListDto())
    {
        Description = product.Description,
        Variants = product.Variants.Select(v => v.ToVariantDto()).ToList(),
    };

    public static ProductVariantDto ToVariantDto(this ProductVariant variant) => new()
    {
        Id = variant.Id,
        Size = variant.Size,
        Color = variant.Color,
        Sku = variant.SKU,
        StockQuantity = variant.StockQuantity,
        IsInStock = variant.IsInStock,
    };

    public static AdminProductDto ToAdminDto(this ManagedProduct managed)
    {
        var product = managed.Product;
        return new AdminProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            CategoryId = product.CategoryId,
            Category = product.Category.Name,
            CategoryLabel = product.Category.Label,
            Fabric = product.Fabric,
            Price = product.Price,
            Currency = DefaultCurrency,
            CoverImage = product.CoverImage,
            Images = MapImages(product),
            IsFeatured = product.IsFeatured,
            IsActive = product.IsActive,
            TotalStock = product.Variants.Sum(v => v.StockQuantity),
            ReviewCount = managed.ReviewCount,
            HasOrders = managed.HasOrders,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Variants = product.Variants
                .OrderBy(v => v.SortOrder)
                .Select(v => new AdminProductVariantDto
                {
                    Id = v.Id,
                    Size = v.Size,
                    Color = v.Color,
                    Sku = v.SKU,
                    StockQuantity = v.StockQuantity,
                    HasOrders = managed.OrderedVariantIds.Contains(v.Id),
                })
                .ToList(),
        };
    }

    public static IReadOnlyList<ProductListDto> ToListDtos(this IEnumerable<Product> products) =>
        products.Select(p => p.ToListDto()).ToList();

    // A product loaded without its gallery (none should be) still shows its cover photo.
    private static List<ProductImageDto> MapImages(Product product) =>
        product.Images.Count == 0
            ? [new ProductImageDto { Url = product.CoverImage }]
            : product.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageDto { Url = i.Url, Color = i.Color })
                .ToList();

    private static List<ProductColorDto> MapColors(Product product) =>
        product.Variants
            .Select(v => v.Color)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(color => new ProductColorDto { Name = color })
            .ToList();

    private static List<string> MapSizes(Product product) =>
        product.Variants
            .Select(v => v.Size)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SizeRank)
            .ToList();

    // Keys are emitted in size order (XS…XXL) so the serialized object matches the frontend shape.
    private static Dictionary<string, int> MapStock(Product product) =>
        product.Variants
            .GroupBy(v => v.Size, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => SizeRank(g.Key))
            .ToDictionary(g => g.Key, g => g.Sum(v => v.StockQuantity), StringComparer.OrdinalIgnoreCase);

    private static int SizeRank(string size)
    {
        var index = Array.FindIndex(SizeOrder, s => s.Equals(size, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? SizeOrder.Length : index;
    }
}
