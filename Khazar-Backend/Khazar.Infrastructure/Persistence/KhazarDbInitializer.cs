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

        // Categories first: every product references one. Existing rows (e.g. created by an admin) are reused by Name.
        var categories = await context.Categories.ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var seed in CategorySeed.Where(seed => !categories.ContainsKey(seed.Name)))
        {
            var category = new Category(seed.Name, seed.Label, seed.Slug, seed.SortOrder);
            categories[category.Name] = category;
            await context.Categories.AddAsync(category, cancellationToken);
        }

        var products = CatalogSeed.Select(seed => BuildProduct(seed, categories[seed.Category])).ToList();

        await context.Products.AddRangeAsync(products, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Seeded {ProductCount} products with {VariantCount} variants.",
            products.Count,
            products.Sum(p => p.Variants.Count));
    }

    private static Product BuildProduct(SeedProduct seed, Category category)
    {
        var product = new Product(
            seed.Name,
            seed.Slug,
            seed.Description,
            seed.Price,
            seed.Fabric,
            category,
            UnsplashImage(seed.CoverImageId),
            seed.IsFeatured);
        product.SetHoverImage(UnsplashImage(seed.HoverImageId));

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

        if (GallerySeed.TryGetValue(seed.Slug, out var gallery))
            product.ReplaceImages(gallery.Select(photo => (UnsplashImage(photo.PhotoId), (string?)photo.Color)).ToList());

        return product;
    }

    /// <summary>A launch photo in wwwroot/images, named after its Unsplash photo id.</summary>
    private static string UnsplashImage(string photoId) => $"/images/photo-{photoId}.jpg";

    /// <summary>
    /// One catalog entry. Category is a <see cref="CategorySeed"/> Name. IsFeatured maps to the storefront's isBestseller
    /// flag. HoverImageId is a second photo of the same garment (often its second color) the catalog card fades to on hover. AddProductHoverImage and
    /// RefreshSeedProductImages bring an already-seeded catalog to these ids.
    /// </summary>
    private sealed record SeedProduct(
        string SkuCode,
        string Name,
        string Slug,
        string Description,
        string Category,
        decimal Price,
        string Fabric,
        string CoverImageId,
        string HoverImageId,
        bool IsFeatured,
        string[] Colors,
        (string Size, int Stock)[] StockBySize);

    /// <summary>
    /// One category. Slugs match the ?kateqoriya= links in the storefront's Header and Footer, labels the names the
    /// storefront showed before categories had their own table. AddCategories creates the same rows for an existing catalog.
    /// </summary>
    private sealed record SeedCategory(string Name, string Label, string Slug, int SortOrder);

    private static readonly SeedCategory[] CategorySeed =
    [
        new("Hoodie", "Hoodie", "hoodie", 10),
        new("T-Shirt", "T-Shirt", "t-shirt", 20),
        new("Pants", "Şalvar", "salvar", 30),
        new("Sweatshirt", "Sweatshirt", "sweatshirt", 40),
        new("Longsleeve", "Longsleeve", "longsleeve", 50),
        new("Coat", "Palto", "palto", 60),
    ];

    /// <summary>
    /// The launch gallery of each product, in display order, with the variant color each photo shows. It replaces the
    /// CoverImageId / HoverImageId pair, which only an older database still holds until an admin edits the product.
    /// </summary>
    private static readonly Dictionary<string, (string PhotoId, string Color)[]> GallerySeed = new(StringComparer.Ordinal)
    {
        ["double-face-coat"] = [("1678700266327-8959be9622fd", "Dəvə"), ("1539533113208-f6df8cc8b543", "Dəvə"), ("1705798543468-5b951da25e1e", "Qara")],
        ["essential-oversize-t-shirt"] = [("1663573689907-23811551910b", "Ağ"), ("1661181475147-bbd20ef65781", "Ağ"), ("1789110854055-fc924935fd09", "Qara")],
        ["european-cotton-t-shirt"] = [("1574180566232-aaad1b5b8450", "Ağ"), ("1622445272461-c6580cab8755", "Ağ"), ("1781106476595-ac88cc3695f2", "Haki")],
        ["heavyweight-hoodie"] = [("1607860087860-c46e865f6ab0", "Antrasit"), ("1738486260590-23c954cf29b8", "Krem")],
        ["merino-longsleeve"] = [("1667829705955-279308e7fc12", "Krem"), ("1752860192499-7320783f73ff", "Krem"), ("1624286922676-24e42c1ceff0", "Tünd yaşıl")],
        ["minimalist-cargo-pants"] = [("1624043553718-f369dab781aa", "Qum"), ("1699970218723-6cf5e76846ba", "Qum"), ("1700676195086-81b936390de4", "Tünd boz"), ("1700676195099-75103510df39", "Tünd boz")],
        ["pleated-wide-pants"] = [("1767631338127-8cd80ee2f9df", "Qara"), ("1770909792040-ccfac330491d", "Qara"), ("1755809869478-87cee105dd54", "Boz"), ("1739967347590-ffffc39b1956", "Boz")],
        ["tailored-wool-coat"] = [("1619603364937-8d7af41ef206", "Dəvə"), ("1619603364904-c0498317e145", "Dəvə"), ("1588621467073-19aa8c4e822c", "Qara")],
        ["vintage-wash-sweatshirt"] = [("1599363987217-605ae31a9695", "Tozlu mavi"), ("1635367096580-f52cc0964a4b", "Qəhvəyi")],
        ["zoho-oversize-sweatshirt"] = [("1554568218-9a456cea00cf", "Boz"), ("1709539881608-628392d557af", "Boz"), ("1605122897309-17343bb5bfb8", "Bej"), ("1661110546801-cfe548af5705", "Qara")],
    };

    private static readonly SeedProduct[] CatalogSeed =
    [
        new("ZOHO", "Zoho Oversize Sweatshirt", "zoho-oversize-sweatshirt",
            "Avropa üç ipliyi ilə toxunmuş, möhkəm mansetli oversize fit svitshot. Limited seriya.",
            "Sweatshirt", 84m, "%80 Pambıq, %20 Polyester", "1661110546801-cfe548af5705", "1630254688956-40da9f30216a",
            IsFeatured: true,
            ["Boz", "Bej", "Qara"],
            [("XS", 4), ("S", 12), ("M", 20), ("L", 8), ("XL", 3)]),

        new("EUCT", "European Cotton T-Shirt", "european-cotton-t-shirt",
            "Yüksək qramajlı Avropa pambığından, yaxa hissəsi gücləndirilmiş klassik fit t-shirt.",
            "T-Shirt", 39.9m, "%100 Pambıq", "1778671394516-8270eac13c42", "1581655353564-df123a1eb820",
            IsFeatured: false,
            ["Ağ", "Qara", "Haki"],
            [("S", 15), ("M", 25), ("L", 18), ("XL", 10), ("XXL", 6)]),

        new("CRGO", "Minimalist Cargo Pants", "minimalist-cargo-pants",
            "Geniş bacaqlı, düşük profilli cib detallı minimalist cargo şalvar. Toka ilə tənzimlənən bel.",
            "Pants", 98m, "%97 Pambıq, %3 Elastan", "1700676195086-81b936390de4", "1548883354-7622d03aca27",
            IsFeatured: false,
            ["Qum", "Tünd boz"],
            [("S", 7), ("M", 14), ("L", 11), ("XL", 5)]),

        new("HVHD", "Heavyweight Hoodie", "heavyweight-hoodie",
            "400 q/m² ağır parça, ön cəbi və ikiqat qapşonlu premium hoodie. Fırçalanmış iç səth.",
            "Hoodie", 89m, "%85 Pambıq, %15 Polyester", "1688111421205-a0a85415b224", "1556821840-3a63f95609a7",
            IsFeatured: false,
            ["Antrasit", "Krem"],
            [("S", 9), ("M", 16), ("L", 12), ("XL", 7), ("XXL", 2)]),

        new("MRNO", "Merino Longsleeve", "merino-longsleeve",
            "İncə merino yun qarışımlı, dəriyə yumşaq toxunan longsleeve. Mövsüm keçidinin əsas parçası.",
            "Longsleeve", 64m, "%50 Merino yun, %50 Viskoz", "1631541909061-71e349d1f203", "1536992266094-82847e1fd431",
            IsFeatured: false,
            ["Krem", "Tünd yaşıl"],
            [("S", 6), ("M", 10), ("L", 9), ("XL", 4)]),

        new("TWCT", "Tailored Wool Coat", "tailored-wool-coat",
            "Klassik kişi siluetli, yun qarışımlı palto. Sıx tikişli yaxa, gizli düyməli bağlanma.",
            "Coat", 289m, "%70 Yun, %30 Polyamid", "1619603364937-8d7af41ef206", "1539533018447-63fcce2678e3",
            IsFeatured: true,
            ["Dəvə", "Qara"],
            [("S", 3), ("M", 8), ("L", 6), ("XL", 2)]),

        new("VWSW", "Vintage Wash Sweatshirt", "vintage-wash-sweatshirt",
            "Köhnəlmə (vintage wash) effektli, yumuşaq toxunuşlu svitshot. Düşük omuz, rahat fit.",
            "Sweatshirt", 79m, "%100 Pambıq", "1599363987217-605ae31a9695", "1697425603592-23863028592b",
            IsFeatured: false,
            ["Tozlu mavi", "Qəhvəyi"],
            [("S", 11), ("M", 17), ("L", 9), ("XL", 5)]),

        new("EOTS", "Essential Oversize T-Shirt", "essential-oversize-t-shirt",
            "Gündəlik istifadə üçün hazırlanmış düşük omuzlu oversize t-shirt. Limitsiz kombin.",
            "T-Shirt", 44m, "%100 Pambıq", "1521572163474-6864f9cf17ab", "1574180566232-aaad1b5b8450",
            IsFeatured: false,
            ["Ağ", "Bej", "Qara"],
            [("XS", 5), ("S", 14), ("M", 22), ("L", 16), ("XL", 9), ("XXL", 4)]),

        new("PLWP", "Pleated Wide Pants", "pleated-wide-pants",
            "Qatlı (pleated) bel detaylı, geniş bacaqlı klassik şalvar. İstirahət və ofis üçün uyğun.",
            "Pants", 112m, "%64 Poliester, %34 Viskoz, %2 Elastan", "1767631338127-8cd80ee2f9df", "1770909792040-ccfac330491d",
            IsFeatured: false,
            ["Qara", "Boz"],
            [("S", 8), ("M", 13), ("L", 10), ("XL", 6)]),

        new("DFCT", "Double-Face Coat", "double-face-coat",
            "İki üzlü yun parçadan, içi kürklə əvəzlənən isti qış paltosu. Minimal kəsik, maksimal rahatlıq.",
            "Coat", 340m, "%80 Yun, %20 Kaşmir", "1770644935626-f61d233f4827", "1701707243530-6d5c5629f150",
            IsFeatured: false,
            ["Qara", "Dəvə"],
            [("S", 2), ("M", 6), ("L", 5), ("XL", 1)]),
    ];
}
