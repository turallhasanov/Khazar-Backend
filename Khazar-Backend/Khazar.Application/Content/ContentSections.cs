using System.ComponentModel.DataAnnotations;

namespace Khazar.Application.Content;

// Typed shape of every editable content section. The storefront reads these as camelCase JSON from GET /api/content;
// the admin panel saves them through PUT /api/admin/content/sections/{key}. ContentValidator applies the limits:
// strings are trimmed and at most [MaxLength] (default 500) characters, lists at most [MaxLength] (default 30) items,
// and properties ending in "Url", "Link" or "To" must be a site path ("/katalog") or an http(s) address.
// Text marked with *asterisks* is shown as the italic accent by the storefront.

/// <summary>A navigation link, e.g. { "label": "Haqqımızda", "to": "/haqqimizda" }.</summary>
public sealed record NavigationLink
{
    [MaxLength(40)]
    public string Label { get; init; } = string.Empty;

    [MaxLength(300)]
    public string To { get; init; } = string.Empty;
}

/// <summary>
/// Site-wide settings: the desktop header links next to "Kataloq", and the contact details and footer copy shared by
/// the footer and the contact page.
/// </summary>
public sealed record SiteSettingsSection
{
    /// <summary>Desktop header links after "Kataloq"; a category is linked as "/katalog?kateqoriya={slug}".</summary>
    [MaxLength(4)]
    public List<NavigationLink> HeaderLinks { get; init; } = [];

    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string WorkingHours { get; init; } = string.Empty;

    /// <summary>Footer heading, e.g. "Get *in* touch".</summary>
    public string FooterHeading { get; init; } = string.Empty;

    public string Copyright { get; init; } = string.Empty;
    public string FooterNote { get; init; } = string.Empty;
}

/// <summary>Home page hero. With <see cref="ProductSlug"/> set, the color, size and price picker shows that product.</summary>
public sealed record HeroSection
{
    public string Kicker { get; init; } = string.Empty;
    public string TitleTop { get; init; } = string.Empty;
    public string TitleLine1 { get; init; } = string.Empty;
    public string TitleLine2 { get; init; } = string.Empty;

    [MaxLength(150)]
    public string ProductSlug { get; init; } = string.Empty;

    /// <summary>Shown instead of the product's price, e.g. "84,00 ₼"; empty uses the product's price.</summary>
    public string PriceLabel { get; init; } = string.Empty;

    public string CtaLabel { get; init; } = string.Empty;

    /// <summary>Empty opens the hero product's page.</summary>
    public string CtaLink { get; init; } = string.Empty;

    public string SecondaryLinkLabel { get; init; } = string.Empty;
    public string SecondaryLinkTo { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string ImageUrl { get; init; } = string.Empty;

    public string ImageAlt { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string SideImageUrl { get; init; } = string.Empty;

    public string SideImageAlt { get; init; } = string.Empty;

    /// <summary>Captions under the main photo, e.g. "Look 01" and "FW 26".</summary>
    public string CaptionLeft { get; init; } = string.Empty;

    public string CaptionRight { get; init; } = string.Empty;

    /// <summary>Vertical signature beside the photos, e.g. "Khazar Studio — Bakı".</summary>
    public string Signature { get; init; } = string.Empty;
}

/// <summary>"New arrivals" stage under the hero; the products themselves are the catalog's newest.</summary>
public sealed record NewArrivalsSection
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string LinkLabel { get; init; } = string.Empty;
    public string LinkTo { get; init; } = string.Empty;
}

public sealed record ManifestoSection
{
    public string Eyebrow { get; init; } = string.Empty;

    /// <summary>Line breaks start a new line of the heading.</summary>
    public string Title { get; init; } = string.Empty;

    [MaxLength(6)]
    public List<string> Paragraphs { get; init; } = [];

    public string Signature { get; init; } = string.Empty;
}

/// <summary>Heading of the lookbook; the looks are managed separately (/api/admin/lookbook-looks).</summary>
public sealed record LookbookSection
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

/// <summary>The dark slogan band, one heading line per entry.</summary>
public sealed record SloganSection
{
    [MaxLength(6)]
    public List<string> Lines { get; init; } = [];
}

public sealed record FeatureBlock
{
    public string Title { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Text { get; init; } = string.Empty;

    public string LinkLabel { get; init; } = string.Empty;
    public string LinkTo { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string ImageUrl { get; init; } = string.Empty;
}

/// <summary>The two info blocks under the slogan band (shipping and FAQ). Only the FAQ block shows an image.</summary>
public sealed record FeatureBlocksSection
{
    public FeatureBlock Shipping { get; init; } = new();
    public FeatureBlock Faq { get; init; } = new();
}

public sealed record TitledText
{
    public string Title { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Text { get; init; } = string.Empty;
}

public sealed record CraftsmanshipSection
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    [MaxLength(1500)]
    public string Text { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string ImageUrl { get; init; } = string.Empty;

    public string ImageAlt { get; init; } = string.Empty;

    [MaxLength(6)]
    public List<TitledText> Highlights { get; init; } = [];
}

/// <summary>Heading of the journal; the articles are managed separately (/api/admin/journal-articles).</summary>
public sealed record JournalSection
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

/// <summary>Cover of the catalog page (/katalog).</summary>
public sealed record CatalogSection
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string CoverImageUrl { get; init; } = string.Empty;

    public string CoverImageAlt { get; init; } = string.Empty;
}

/// <summary>Header shared by the static pages (eyebrow, title, description).</summary>
public sealed record PageHeader
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; init; } = string.Empty;
}

/// <summary>About page (/haqqimizda).</summary>
public sealed record AboutSection
{
    public PageHeader Header { get; init; } = new();

    [MaxLength(1000)]
    public string CoverImageUrl { get; init; } = string.Empty;

    public string CoverImageAlt { get; init; } = string.Empty;
    public string CoverCaption { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Quote { get; init; } = string.Empty;

    public string QuoteAuthor { get; init; } = string.Empty;

    [MaxLength(6)]
    public List<TitledText> Pillars { get; init; } = [];

    public string CtaEyebrow { get; init; } = string.Empty;
    public string CtaTitle { get; init; } = string.Empty;
    public string CtaLabel { get; init; } = string.Empty;
    public string CtaLink { get; init; } = string.Empty;
}

/// <summary>Header of the FAQ page; the questions are managed separately (/api/admin/faq-items).</summary>
public sealed record FaqPageSection
{
    public PageHeader Header { get; init; } = new();
}

public sealed record DeliveryRow
{
    public string Region { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public string Fee { get; init; } = string.Empty;
}

/// <summary>Shipping and returns page (/catdirilma-ve-iade).</summary>
public sealed record ShippingSection
{
    public PageHeader Header { get; init; } = new();
    public string DeliveryTitle { get; init; } = string.Empty;

    [MaxLength(12)]
    public List<DeliveryRow> DeliveryRows { get; init; } = [];

    [MaxLength(1000)]
    public string DeliveryNote { get; init; } = string.Empty;

    public string ReturnsTitle { get; init; } = string.Empty;

    [MaxLength(6)]
    public List<TitledText> ReturnSteps { get; init; } = [];
}

/// <summary>Contact page (/elaqe); the phone, email, address and hours come from <see cref="SiteSettingsSection"/>.</summary>
public sealed record ContactSection
{
    public PageHeader Header { get; init; } = new();
    public string ShowroomTitle { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string ShowroomNote { get; init; } = string.Empty;
}

public sealed record SizeGuideRow
{
    [MaxLength(20)]
    public string Size { get; init; } = string.Empty;

    [MaxLength(30)]
    public string Chest { get; init; } = string.Empty;

    [MaxLength(30)]
    public string Waist { get; init; } = string.Empty;

    [MaxLength(30)]
    public string Neck { get; init; } = string.Empty;
}

/// <summary>Size guide opened from the product page.</summary>
public sealed record SizeGuideSection
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Text { get; init; } = string.Empty;

    [MaxLength(12)]
    public List<SizeGuideRow> Rows { get; init; } = [];

    /// <summary>Size the model in the photos wears; its row is highlighted. Empty highlights none.</summary>
    [MaxLength(20)]
    public string ModelSize { get; init; } = string.Empty;

    /// <summary>Note under the table, e.g. "Model 185 sm boyundadır və M ölçü geyinib."</summary>
    public string ModelNote { get; init; } = string.Empty;
}

public sealed record PolicyBlock
{
    [MaxLength(200)]
    public string Heading { get; init; } = string.Empty;

    [MaxLength(12)]
    public List<string> Paragraphs { get; init; } = [];
}

/// <summary>Privacy policy page (/gizlilik-siyaseti).</summary>
public sealed record PrivacySection
{
    public PageHeader Header { get; init; } = new();

    /// <summary>Shown above the text, e.g. "Son yenilənmə: 31 oktyabr 2026".</summary>
    public string LastUpdated { get; init; } = string.Empty;

    [MaxLength(20)]
    public List<PolicyBlock> Blocks { get; init; } = [];
}

/// <summary>Suggestions shown in the search window before the shopper types.</summary>
public sealed record SearchSection
{
    [MaxLength(10)]
    public List<string> TrendingSearches { get; init; } = [];
}
