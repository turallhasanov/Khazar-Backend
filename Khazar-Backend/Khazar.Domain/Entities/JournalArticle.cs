using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>
/// A journal ("Jurnal") article: the card on the home page and the page at /journal/{slug}. The body is a JSON list
/// of sections (heading, paragraphs, optional image) whose shape the application layer validates.
/// </summary>
public class JournalArticle : BaseEntity
{
    public const int MaxSlugLength = 150;
    public const int MaxTitleLength = 200;
    public const int MaxExcerptLength = 500;
    public const int MaxMetaLength = 100;
    public const int MaxImageUrlLength = 1000;

    public string Slug { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Excerpt { get; private set; } = default!;
    public string CoverImageUrl { get; private set; } = default!;

    /// <summary>Caption above the title, e.g. "Dərgi · 5 dəqiqə".</summary>
    public string Meta { get; private set; } = default!;

    public string Body { get; private set; } = default!;

    /// <summary>Position on the home page; lower comes first.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Unpublished articles are visible in the admin panel only.</summary>
    public bool IsPublished { get; private set; }

    // Required by ORMs
    private JournalArticle() { }

    public JournalArticle(
        string slug, string title, string excerpt, string coverImageUrl, string meta, string body, int sortOrder, bool isPublished)
    {
        SetDetails(slug, title, excerpt, coverImageUrl, meta, body, sortOrder, isPublished);
    }

    public void UpdateDetails(
        string slug, string title, string excerpt, string coverImageUrl, string meta, string body, int sortOrder, bool isPublished)
    {
        SetDetails(slug, title, excerpt, coverImageUrl, meta, body, sortOrder, isPublished);
        MarkUpdated();
    }

    private void SetDetails(
        string slug, string title, string excerpt, string coverImageUrl, string meta, string body, int sortOrder, bool isPublished)
    {
        Slug = Guard.MaxLength(Guard.NotEmpty(slug, nameof(Slug)), MaxSlugLength, nameof(Slug)).ToLowerInvariant();
        Title = Guard.MaxLength(Guard.NotEmpty(title, nameof(Title)), MaxTitleLength, nameof(Title));
        Excerpt = Guard.MaxLength(excerpt?.Trim() ?? string.Empty, MaxExcerptLength, nameof(Excerpt));
        CoverImageUrl = Guard.MaxLength(Guard.NotEmpty(coverImageUrl, nameof(CoverImageUrl)), MaxImageUrlLength, nameof(CoverImageUrl));
        Meta = Guard.MaxLength(meta?.Trim() ?? string.Empty, MaxMetaLength, nameof(Meta));
        Body = Guard.NotEmpty(body, nameof(Body));
        SortOrder = Guard.NotNegative(sortOrder, nameof(SortOrder));
        IsPublished = isPublished;
    }
}
