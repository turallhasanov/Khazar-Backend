using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Khazar.Infrastructure.Persistence;

/// <summary>
/// Seeds the editorial catalog into an empty database.
/// The data mirrors Khazar-Frontend/src/data/mockProducts.js: names, slugs, descriptions, prices,
/// categories, colors, sizes and per-size stock are copied verbatim, so the API returns the same
/// catalog the frontend was built against.
/// </summary>
public static class KhazarDbInitializer
{
    private const string SkuPrefix = "KHZ";

    // Color names are kept in Azerbaijani as in the storefront (checkout matches variants by color name).
    // SKUs use short English codes instead, e.g. "Qara" -> "BLK".
    private static readonly Dictionary<string, string> ColorCodes = new(StringComparer.Ordinal)
    {
        ["Ağ"] = "WHT",
        ["Antrasit"] = "ANT",
        ["Bej"] = "BEI",
        ["Boz"] = "GRY",
        ["Dəvə"] = "CML",
        ["Haki"] = "KHK",
        ["Krem"] = "CRM",
        ["Qara"] = "BLK",
        ["Qəhvəyi"] = "BRN",
        ["Qum"] = "SND",
        ["Tozlu mavi"] = "DBL",
        ["Tünd boz"] = "CHR",
        ["Tünd yaşıl"] = "DGR",
    };

    public static async Task SeedAsync(
        KhazarDbContext context,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await context.Products.AnyAsync(cancellationToken))
        {
            logger?.LogInformation("Catalog already contains products; seeding skipped.");
            return;
        }

        var products = CatalogSeed.Select(BuildProduct).ToList();

        await context.Products.AddRangeAsync(products, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Seeded {ProductCount} products with {VariantCount} variants.",
            products.Count,
            products.Sum(p => p.Variants.Count));
    }

    private static Product BuildProduct(SeedProduct seed)
    {
        var product = new Product(
            seed.Name,
            seed.Slug,
            seed.Description,
            seed.Price,
            seed.Fabric,
            seed.Category,
            UnsplashImage(seed.CoverImageId),
            seed.IsFeatured);

        foreach (var (size, sizeStock) in seed.StockBySize)
        {
            // The frontend tracks stock per size only; split it across colors so that the
            // per-size totals returned by the API equal the mock values exactly.
            var colorCount = seed.Colors.Length;
            for (var i = 0; i < colorCount; i++)
            {
                var color = seed.Colors[i];
                var quantity = sizeStock / colorCount + (i < sizeStock % colorCount ? 1 : 0);
                var sku = $"{SkuPrefix}-{seed.SkuCode}-{ColorCodes[color]}-{size}";

                product.AddVariant(size, color, sku, quantity);
            }
        }

        return product;
    }

    private static string UnsplashImage(string photoId) =>
        $"https://images.unsplash.com/photo-{photoId}?auto=format&fit=crop&w=900&q=80";

    /// <summary>One catalog entry. IsFeatured maps to the storefront's isBestseller flag.</summary>
    private sealed record SeedProduct(
        string SkuCode,
        string Name,
        string Slug,
        string Description,
        string Category,
        decimal Price,
        string Fabric,
        string CoverImageId,
        bool IsFeatured,
        string[] Colors,
        (string Size, int Stock)[] StockBySize);

    private static readonly SeedProduct[] CatalogSeed =
    [
        new("ZOHO", "Zoho Oversize Sweatshirt", "zoho-oversize-sweatshirt",
            "Avropa üç ipliyi ilə toxunmuş, möhkəm mansetli oversize fit svitshot. Limited seriya.",
            "Sweatshirt", 84m, "%80 Pambıq, %20 Polyester", "1576871337622-98d48d1cf531", IsFeatured: true,
            ["Boz", "Bej", "Qara"],
            [("XS", 4), ("S", 12), ("M", 20), ("L", 8), ("XL", 3)]),

        new("EUCT", "European Cotton T-Shirt", "european-cotton-t-shirt",
            "Yüksək qramajlı Avropa pambığından, yaxa hissəsi gücləndirilmiş klassik fit t-shirt.",
            "T-Shirt", 39.9m, "%100 Pambıq", "1521572163474-6864f9cf17ab", IsFeatured: false,
            ["Ağ", "Qara", "Haki"],
            [("S", 15), ("M", 25), ("L", 18), ("XL", 10), ("XXL", 6)]),

        new("CRGO", "Minimalist Cargo Pants", "minimalist-cargo-pants",
            "Geniş bacaqlı, düşük profilli cib detallı minimalist cargo şalvar. Toka ilə tənzimlənən bel.",
            "Pants", 98m, "%97 Pambıq, %3 Elastan", "1542272604-787c3835535d", IsFeatured: false,
            ["Qum", "Tünd boz"],
            [("S", 7), ("M", 14), ("L", 11), ("XL", 5)]),

        new("HVHD", "Heavyweight Hoodie", "heavyweight-hoodie",
            "400 q/m² ağır parça, ön cəbi və ikiqat qapşonlu premium hoodie. Fırçalanmış iç səth.",
            "Hoodie", 89m, "%85 Pambıq, %15 Polyester", "1620799140408-edc6dcb6d633", IsFeatured: false,
            ["Antrasit", "Krem"],
            [("S", 9), ("M", 16), ("L", 12), ("XL", 7), ("XXL", 2)]),

        new("MRNO", "Merino Longsleeve", "merino-longsleeve",
            "İncə merino yun qarışımlı, dəriyə yumşaq toxunan longsleeve. Mövsüm keçidinin əsas parçası.",
            "Longsleeve", 64m, "%50 Merino yun, %50 Viskoz", "1434389677669-e08b4cac3105", IsFeatured: false,
            ["Krem", "Tünd yaşıl"],
            [("S", 6), ("M", 10), ("L", 9), ("XL", 4)]),

        new("TWCT", "Tailored Wool Coat", "tailored-wool-coat",
            "Klassik kişi siluetli, yun qarışımlı palto. Sıx tikişli yaxa, gizli düyməli bağlanma.",
            "Coat", 289m, "%70 Yun, %30 Polyamid", "1539533018447-63fcce2678e3", IsFeatured: true,
            ["Dəvə", "Qara"],
            [("S", 3), ("M", 8), ("L", 6), ("XL", 2)]),

        new("VWSW", "Vintage Wash Sweatshirt", "vintage-wash-sweatshirt",
            "Köhnəlmə (vintage wash) effektli, yumuşaq toxunuşlu svitshot. Düşük omuz, rahat fit.",
            "Sweatshirt", 79m, "%100 Pambıq", "1620799140408-edc6dcb6d633", IsFeatured: false,
            ["Tozlu mavi", "Qəhvəyi"],
            [("S", 11), ("M", 17), ("L", 9), ("XL", 5)]),

        new("EOTS", "Essential Oversize T-Shirt", "essential-oversize-t-shirt",
            "Gündəlik istifadə üçün hazırlanmış düşük omuzlu oversize t-shirt. Limitsiz kombin.",
            "T-Shirt", 44m, "%100 Pambıq", "1562157873-818bc0726f68", IsFeatured: false,
            ["Ağ", "Bej", "Qara"],
            [("XS", 5), ("S", 14), ("M", 22), ("L", 16), ("XL", 9), ("XXL", 4)]),

        new("PLWP", "Pleated Wide Pants", "pleated-wide-pants",
            "Qatlı (pleated) bel detaylı, geniş bacaqlı klassik şalvar. İstirahət və ofis üçün uyğun.",
            "Pants", 112m, "%64 Poliester, %34 Viskoz, %2 Elastan", "1594633312681-425c7b97ccd1", IsFeatured: false,
            ["Qara", "Boz"],
            [("S", 8), ("M", 13), ("L", 10), ("XL", 6)]),

        new("DFCT", "Double-Face Coat", "double-face-coat",
            "İki üzlü yun parçadan, içi kürklə əvəzlənən isti qış paltosu. Minimal kəsik, maksimal rahatlıq.",
            "Coat", 340m, "%80 Yun, %20 Kaşmir", "1551028719-00167b16eac5", IsFeatured: false,
            ["Qara", "Dəvə"],
            [("S", 2), ("M", 6), ("L", 5), ("XL", 1)]),
    ];
}
