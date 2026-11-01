using Khazar.Application.DTOs.Content;

namespace Khazar.Application.Services;

/// <summary>Storefront reads of the content managed in the admin panel. Only published rows are returned.</summary>
public interface ISiteContentAppService
{
    Task<SiteContentDto> GetSiteContentAsync(CancellationToken cancellationToken = default);

    /// <summary>The published article with this slug, or null.</summary>
    Task<JournalArticleDto?> GetPublishedArticleAsync(string slug, CancellationToken cancellationToken = default);
}
