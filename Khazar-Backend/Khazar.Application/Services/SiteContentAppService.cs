using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.Content;
using Khazar.Application.DTOs.Content;

namespace Khazar.Application.Services;

public sealed class SiteContentAppService(
    IContentSectionRepository sectionRepository,
    IJournalArticleRepository articleRepository,
    ILookbookLookRepository lookRepository,
    IFaqItemRepository faqRepository) : ISiteContentAppService
{
    public async Task<SiteContentDto> GetSiteContentAsync(CancellationToken cancellationToken = default)
    {
        // One DbContext per request: the queries run one after another, not in parallel.
        var sections = await sectionRepository.GetAllAsync(cancellationToken);
        var looks = await lookRepository.GetPublishedAsync(cancellationToken);
        var articles = await articleRepository.GetPublishedAsync(cancellationToken);
        var faq = await faqRepository.GetPublishedAsync(cancellationToken);

        var data = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var section in sections)
        {
            // Rows of keys this version no longer knows, or with unreadable data, are skipped rather than failing the site.
            if (ContentSchema.IsKnown(section.Key) && ContentSchema.Read(section.Key, section.Data) is { } value)
                data[section.Key] = value;
        }

        return new SiteContentDto
        {
            Sections = data,
            Lookbook = looks.Select(look => look.ToDto()).ToList(),
            Journal = articles.Select(article => article.ToSummaryDto()).ToList(),
            Faq = faq.Select(item => item.ToDto()).ToList(),
        };
    }

    public async Task<JournalArticleDto?> GetPublishedArticleAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var article = await articleRepository.GetPublishedBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken);
        return article?.ToDto();
    }
}
