using System.ComponentModel.DataAnnotations;

namespace Khazar.Application.DTOs.Content;

// Request DTOs are checked by ContentValidator (see its rules); [MaxLength] on a string is its character limit,
// on a list its item limit.

/// <summary>Everything the storefront shows from the content tables, in one response (GET /api/content).</summary>
public sealed record SiteContentDto
{
    /// <summary>Saved sections by key ("hero", "about", ...); a section that was never saved is absent.</summary>
    public IReadOnlyDictionary<string, object> Sections { get; init; } = new Dictionary<string, object>();

    /// <summary>Published looks in lookbook order.</summary>
    public IReadOnlyList<LookbookLookDto> Lookbook { get; init; } = [];

    /// <summary>Published articles in home page order, without their body.</summary>
    public IReadOnlyList<JournalArticleSummaryDto> Journal { get; init; } = [];

    /// <summary>Published questions in FAQ page order.</summary>
    public IReadOnlyList<FaqItemDto> Faq { get; init; } = [];
}

/// <summary>A content section as the admin panel edits it.</summary>
public sealed record ContentSectionDto
{
    public string Key { get; init; } = string.Empty;

    /// <summary>The section's fields; every field empty when it was never saved.</summary>
    public object Data { get; init; } = new();

    /// <summary>Last save; null when the section was never saved (the storefront hides it).</summary>
    public DateTime? UpdatedAt { get; init; }
}

public sealed record ArticleSectionDto
{
    [MaxLength(200)]
    public string Heading { get; init; } = string.Empty;

    [MaxLength(12)]
    public List<string> Paragraphs { get; init; } = [];

    [MaxLength(1000)]
    public string ImageUrl { get; init; } = string.Empty;

    [MaxLength(200)]
    public string ImageAlt { get; init; } = string.Empty;
}

public record JournalArticleSummaryDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public string CoverImageUrl { get; init; } = string.Empty;
    public string Meta { get; init; } = string.Empty;
}

public sealed record JournalArticleDto : JournalArticleSummaryDto
{
    public IReadOnlyList<ArticleSectionDto> Sections { get; init; } = [];
    public int SortOrder { get; init; }
    public bool IsPublished { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record SaveJournalArticleRequestDto
{
    /// <summary>URL value of /journal/{slug}: lower-case letters, digits and single hyphens.</summary>
    [MaxLength(150)]
    public string Slug { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(500)]
    public string Excerpt { get; init; } = string.Empty;

    [Required, MaxLength(1000)]
    public string CoverImageUrl { get; init; } = string.Empty;

    /// <summary>Caption above the title, e.g. "Dərgi · 5 dəqiqə".</summary>
    [MaxLength(100)]
    public string Meta { get; init; } = string.Empty;

    [MaxLength(20)]
    public List<ArticleSectionDto> Sections { get; init; } = [];

    [Range(0, 10_000)]
    public int SortOrder { get; init; }

    public bool IsPublished { get; init; }
}

public sealed record LookbookHotspotDto
{
    /// <summary>Slug of the product the hotspot opens.</summary>
    [Required, MaxLength(150)]
    public string ProductSlug { get; init; } = string.Empty;

    /// <summary>Horizontal position, percent of the photo width.</summary>
    [Range(0d, 100d)]
    public double X { get; init; }

    /// <summary>Vertical position, percent of the photo height.</summary>
    [Range(0d, 100d)]
    public double Y { get; init; }
}

public sealed record LookbookLookDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string CtaLabel { get; init; } = string.Empty;
    public string CtaLink { get; init; } = string.Empty;
    public IReadOnlyList<LookbookHotspotDto> Hotspots { get; init; } = [];
    public int SortOrder { get; init; }
    public bool IsPublished { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record SaveLookbookLookRequestDto
{
    [Required, MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(1000)]
    public string ImageUrl { get; init; } = string.Empty;

    [MaxLength(100)]
    public string CtaLabel { get; init; } = string.Empty;

    [MaxLength(300)]
    public string CtaLink { get; init; } = string.Empty;

    [MaxLength(8)]
    public List<LookbookHotspotDto> Hotspots { get; init; } = [];

    [Range(0, 10_000)]
    public int SortOrder { get; init; }

    public bool IsPublished { get; init; }
}

public sealed record FaqItemDto
{
    public Guid Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsPublished { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record SaveFaqItemRequestDto
{
    [Required, MaxLength(300)]
    public string Question { get; init; } = string.Empty;

    [Required, MaxLength(3000)]
    public string Answer { get; init; } = string.Empty;

    [Range(0, 10_000)]
    public int SortOrder { get; init; }

    public bool IsPublished { get; init; }
}

public sealed record ContactMessageDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record CreateContactMessageRequestDto
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public sealed record UpdateContactMessageRequestDto
{
    public bool IsRead { get; init; }
}
