using Khazar.Application.Content;
using Khazar.Application.DTOs.Content;
using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Mappings;

/// <summary>Explicit content entity to DTO mappings. JSON columns that cannot be read map to empty lists.</summary>
public static class ContentMappings
{
    public static JournalArticleSummaryDto ToSummaryDto(this JournalArticle article) => new()
    {
        Id = article.Id,
        Slug = article.Slug,
        Title = article.Title,
        Excerpt = article.Excerpt,
        CoverImageUrl = article.CoverImageUrl,
        Meta = article.Meta,
    };

    public static JournalArticleDto ToDto(this JournalArticle article) => new()
    {
        Id = article.Id,
        Slug = article.Slug,
        Title = article.Title,
        Excerpt = article.Excerpt,
        CoverImageUrl = article.CoverImageUrl,
        Meta = article.Meta,
        Sections = ContentSchema.ReadOrDefault<List<ArticleSectionDto>>(article.Body, []),
        SortOrder = article.SortOrder,
        IsPublished = article.IsPublished,
        CreatedAt = article.CreatedAt,
        UpdatedAt = article.UpdatedAt,
    };

    public static LookbookLookDto ToDto(this LookbookLook look) => new()
    {
        Id = look.Id,
        Title = look.Title,
        ImageUrl = look.ImageUrl,
        CtaLabel = look.CtaLabel,
        CtaLink = look.CtaLink,
        Hotspots = ContentSchema.ReadOrDefault<List<LookbookHotspotDto>>(look.Hotspots, []),
        SortOrder = look.SortOrder,
        IsPublished = look.IsPublished,
        CreatedAt = look.CreatedAt,
        UpdatedAt = look.UpdatedAt,
    };

    public static FaqItemDto ToDto(this FaqItem item) => new()
    {
        Id = item.Id,
        Question = item.Question,
        Answer = item.Answer,
        SortOrder = item.SortOrder,
        IsPublished = item.IsPublished,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
    };

    public static ContactMessageDto ToDto(this ContactMessage message) => new()
    {
        Id = message.Id,
        Name = message.Name,
        Email = message.Email,
        Message = message.Message,
        IsRead = message.IsRead,
        ReadAt = message.ReadAt,
        CreatedAt = message.CreatedAt,
    };
}
