using System.Text.Json;
using System.Text.RegularExpressions;
using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.Content;
using Khazar.Application.DTOs.Content;
using Khazar.Domain.Entities;

namespace Khazar.Application.Services;

public sealed partial class AdminContentAppService(
    IContentSectionRepository sectionRepository,
    IJournalArticleRepository articleRepository,
    ILookbookLookRepository lookRepository,
    IFaqItemRepository faqRepository,
    IProductRepository productRepository) : IAdminContentAppService
{
    // ---------- Sections ----------

    public async Task<IReadOnlyList<ContentSectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default)
    {
        var stored = (await sectionRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Key, StringComparer.Ordinal);

        return ContentSchema.Sections.Keys
            .Select(key => stored.TryGetValue(key, out var section)
                ? new ContentSectionDto
                {
                    Key = key,
                    Data = ContentSchema.Read(key, section.Data) ?? ContentSchema.CreateEmpty(key),
                    UpdatedAt = section.UpdatedAt ?? section.CreatedAt,
                }
                : new ContentSectionDto { Key = key, Data = ContentSchema.CreateEmpty(key) })
            .ToList();
    }

    public async Task<ContentSectionDto> SaveSectionAsync(string key, JsonElement data, CancellationToken cancellationToken = default)
    {
        if (!ContentSchema.IsKnown(key))
            throw new NotFoundException($"Content section '{key}' does not exist.");

        var value = ContentSchema.Parse(key, data);
        var json = ContentSchema.Serialize(value);

        var section = await sectionRepository.GetByKeyAsync(key, cancellationToken);
        if (section is null)
        {
            section = await sectionRepository.AddAsync(new ContentSection(key, json), cancellationToken);
        }
        else
        {
            section.Replace(json);
            await sectionRepository.UpdateAsync(section, cancellationToken);
        }

        return new ContentSectionDto { Key = key, Data = value, UpdatedAt = section.UpdatedAt ?? section.CreatedAt };
    }

    // ---------- Journal articles ----------

    public async Task<IReadOnlyList<JournalArticleDto>> GetArticlesAsync(CancellationToken cancellationToken = default) =>
        (await articleRepository.GetAllAsync(cancellationToken)).Select(article => article.ToDto()).ToList();

    public async Task<JournalArticleDto> GetArticleAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadArticleAsync(id, cancellationToken)).ToDto();

    public async Task<JournalArticleDto> CreateArticleAsync(SaveJournalArticleRequestDto dto, CancellationToken cancellationToken = default)
    {
        var (fields, slug, body) = await ValidateArticleAsync(dto, null, cancellationToken);
        var article = new JournalArticle(
            slug, fields.Title, fields.Excerpt, fields.CoverImageUrl, fields.Meta, body, fields.SortOrder, fields.IsPublished);
        await articleRepository.AddAsync(article, cancellationToken);
        return article.ToDto();
    }

    public async Task<JournalArticleDto> UpdateArticleAsync(
        Guid id, SaveJournalArticleRequestDto dto, CancellationToken cancellationToken = default)
    {
        var article = await LoadArticleAsync(id, cancellationToken);
        var (fields, slug, body) = await ValidateArticleAsync(dto, id, cancellationToken);
        article.UpdateDetails(
            slug, fields.Title, fields.Excerpt, fields.CoverImageUrl, fields.Meta, body, fields.SortOrder, fields.IsPublished);
        await articleRepository.UpdateAsync(article, cancellationToken);
        return article.ToDto();
    }

    public async Task DeleteArticleAsync(Guid id, CancellationToken cancellationToken = default) =>
        await articleRepository.DeleteAsync(await LoadArticleAsync(id, cancellationToken), cancellationToken);

    private async Task<JournalArticle> LoadArticleAsync(Guid id, CancellationToken cancellationToken) =>
        await articleRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(JournalArticle), id);

    private async Task<(SaveJournalArticleRequestDto Fields, string Slug, string Body)> ValidateArticleAsync(
        SaveJournalArticleRequestDto dto, Guid? exceptId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fields = ContentValidator.Normalize(dto);

        var slug = fields.Slug.ToLowerInvariant();
        if (!SlugPattern().IsMatch(slug))
            throw new ValidationException(
                "Slug must use lower-case letters, digits and single hyphens (e.g. \"kapsul-qarderob\").", "invalid_slug");
        if (await articleRepository.SlugExistsAsync(slug, exceptId, cancellationToken))
            throw new ConflictException("Another article already uses this slug.", "article_slug_taken");

        // Blank paragraphs and sections left empty in the editor are dropped instead of rendered as gaps.
        var sections = fields.Sections
            .Select(section => section with { Paragraphs = section.Paragraphs.Where(p => p.Length > 0).ToList() })
            .Where(section => section.Heading.Length > 0 || section.Paragraphs.Count > 0 || section.ImageUrl.Length > 0)
            .ToList();
        if (!sections.Any(section => section.Paragraphs.Count > 0))
            throw new ValidationException("Field 'sections' needs at least one paragraph.", ContentSchema.InvalidContentErrorCode);

        return (fields, slug, ContentSchema.Serialize(sections));
    }

    // ---------- Lookbook looks ----------

    public async Task<IReadOnlyList<LookbookLookDto>> GetLooksAsync(CancellationToken cancellationToken = default) =>
        (await lookRepository.GetAllAsync(cancellationToken)).Select(look => look.ToDto()).ToList();

    public async Task<LookbookLookDto> GetLookAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadLookAsync(id, cancellationToken)).ToDto();

    public async Task<LookbookLookDto> CreateLookAsync(SaveLookbookLookRequestDto dto, CancellationToken cancellationToken = default)
    {
        var (fields, hotspots) = await ValidateLookAsync(dto, cancellationToken);
        var look = new LookbookLook(
            fields.Title, fields.ImageUrl, fields.CtaLabel, fields.CtaLink, hotspots, fields.SortOrder, fields.IsPublished);
        await lookRepository.AddAsync(look, cancellationToken);
        return look.ToDto();
    }

    public async Task<LookbookLookDto> UpdateLookAsync(Guid id, SaveLookbookLookRequestDto dto, CancellationToken cancellationToken = default)
    {
        var look = await LoadLookAsync(id, cancellationToken);
        var (fields, hotspots) = await ValidateLookAsync(dto, cancellationToken);
        look.UpdateDetails(fields.Title, fields.ImageUrl, fields.CtaLabel, fields.CtaLink, hotspots, fields.SortOrder, fields.IsPublished);
        await lookRepository.UpdateAsync(look, cancellationToken);
        return look.ToDto();
    }

    public async Task DeleteLookAsync(Guid id, CancellationToken cancellationToken = default) =>
        await lookRepository.DeleteAsync(await LoadLookAsync(id, cancellationToken), cancellationToken);

    private async Task<LookbookLook> LoadLookAsync(Guid id, CancellationToken cancellationToken) =>
        await lookRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(LookbookLook), id);

    private async Task<(SaveLookbookLookRequestDto Fields, string Hotspots)> ValidateLookAsync(
        SaveLookbookLookRequestDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fields = ContentValidator.Normalize(dto);

        var hotspots = fields.Hotspots.Select(h => h with { ProductSlug = h.ProductSlug.ToLowerInvariant() }).ToList();
        foreach (var slug in hotspots.Select(h => h.ProductSlug).Distinct(StringComparer.Ordinal))
        {
            if (!await productRepository.SlugExistsAsync(slug, cancellationToken: cancellationToken))
                throw new ValidationException($"No product has the slug \"{slug}\".", "invalid_hotspot_product");
        }

        return (fields, ContentSchema.Serialize(hotspots));
    }

    // ---------- FAQ ----------

    public async Task<IReadOnlyList<FaqItemDto>> GetFaqItemsAsync(CancellationToken cancellationToken = default) =>
        (await faqRepository.GetAllAsync(cancellationToken)).Select(item => item.ToDto()).ToList();

    public async Task<FaqItemDto> GetFaqItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadFaqItemAsync(id, cancellationToken)).ToDto();

    public async Task<FaqItemDto> CreateFaqItemAsync(SaveFaqItemRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fields = ContentValidator.Normalize(dto);
        var item = new FaqItem(fields.Question, fields.Answer, fields.SortOrder, fields.IsPublished);
        await faqRepository.AddAsync(item, cancellationToken);
        return item.ToDto();
    }

    public async Task<FaqItemDto> UpdateFaqItemAsync(Guid id, SaveFaqItemRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var item = await LoadFaqItemAsync(id, cancellationToken);
        var fields = ContentValidator.Normalize(dto);
        item.UpdateDetails(fields.Question, fields.Answer, fields.SortOrder, fields.IsPublished);
        await faqRepository.UpdateAsync(item, cancellationToken);
        return item.ToDto();
    }

    public async Task DeleteFaqItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        await faqRepository.DeleteAsync(await LoadFaqItemAsync(id, cancellationToken), cancellationToken);

    private async Task<FaqItem> LoadFaqItemAsync(Guid id, CancellationToken cancellationToken) =>
        await faqRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(FaqItem), id);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
