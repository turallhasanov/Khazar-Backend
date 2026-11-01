using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>
/// One look of the home page lookbook: a photo, a call to action and shoppable hotspots. Hotspots are a JSON list of
/// { productSlug, x, y } (percent of the photo) validated by the application layer.
/// </summary>
public class LookbookLook : BaseEntity
{
    public const int MaxTitleLength = 150;
    public const int MaxImageUrlLength = 1000;
    public const int MaxCtaLabelLength = 100;
    public const int MaxCtaLinkLength = 300;

    public string Title { get; private set; } = default!;
    public string ImageUrl { get; private set; } = default!;
    public string CtaLabel { get; private set; } = default!;
    public string CtaLink { get; private set; } = default!;
    public string Hotspots { get; private set; } = default!;

    /// <summary>Position in the lookbook; lower comes first. The storefront numbers looks by this order.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Unpublished looks are visible in the admin panel only.</summary>
    public bool IsPublished { get; private set; }

    // Required by ORMs
    private LookbookLook() { }

    public LookbookLook(string title, string imageUrl, string ctaLabel, string ctaLink, string hotspots, int sortOrder, bool isPublished)
    {
        SetDetails(title, imageUrl, ctaLabel, ctaLink, hotspots, sortOrder, isPublished);
    }

    public void UpdateDetails(string title, string imageUrl, string ctaLabel, string ctaLink, string hotspots, int sortOrder, bool isPublished)
    {
        SetDetails(title, imageUrl, ctaLabel, ctaLink, hotspots, sortOrder, isPublished);
        MarkUpdated();
    }

    private void SetDetails(string title, string imageUrl, string ctaLabel, string ctaLink, string hotspots, int sortOrder, bool isPublished)
    {
        Title = Guard.MaxLength(Guard.NotEmpty(title, nameof(Title)), MaxTitleLength, nameof(Title));
        ImageUrl = Guard.MaxLength(Guard.NotEmpty(imageUrl, nameof(ImageUrl)), MaxImageUrlLength, nameof(ImageUrl));
        CtaLabel = Guard.MaxLength(ctaLabel?.Trim() ?? string.Empty, MaxCtaLabelLength, nameof(CtaLabel));
        CtaLink = Guard.MaxLength(ctaLink?.Trim() ?? string.Empty, MaxCtaLinkLength, nameof(CtaLink));
        Hotspots = Guard.NotEmpty(hotspots, nameof(Hotspots));
        SortOrder = Guard.NotNegative(sortOrder, nameof(SortOrder));
        IsPublished = isPublished;
    }
}
