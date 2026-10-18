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
        Category = product.Category,
        BasePrice = product.Price,
        Currency = DefaultCurrency,
        Colors = MapColors(product),
        Sizes = MapSizes(product),
        Images = [product.CoverImage],
        Fabric = new ProductFabricDto { Material = product.Fabric },
        Stock = MapStock(product),
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

    public static IReadOnlyList<ProductListDto> ToListDtos(this IEnumerable<Product> products) =>
        products.Select(p => p.ToListDto()).ToList();

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
