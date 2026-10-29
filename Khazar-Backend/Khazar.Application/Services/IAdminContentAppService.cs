using System.Text.Json;
using Khazar.Application.DTOs.Content;

namespace Khazar.Application.Services;

/// <summary>
/// Admin management of the storefront content: the sections (fixed keys, edited in place) and the journal articles,
/// lookbook looks and FAQ items (created, edited and deleted). Validation errors use the code "invalid_content" and
/// name the field; "invalid_slug", "article_slug_taken" and "invalid_hotspot_product" cover the remaining rules.
/// </summary>
public interface IAdminContentAppService
{
    /// <summary>Every known section, saved or not, in the order of <see cref="Content.ContentSchema.Sections"/>.</summary>
    Task<IReadOnlyList<ContentSectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces a section's data; throws NotFoundException for an unknown key.</summary>
    Task<ContentSectionDto> SaveSectionAsync(string key, JsonElement data, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JournalArticleDto>> GetArticlesAsync(CancellationToken cancellationToken = default);
    Task<JournalArticleDto> GetArticleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JournalArticleDto> CreateArticleAsync(SaveJournalArticleRequestDto dto, CancellationToken cancellationToken = default);
    Task<JournalArticleDto> UpdateArticleAsync(Guid id, SaveJournalArticleRequestDto dto, CancellationToken cancellationToken = default);
    Task DeleteArticleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookbookLookDto>> GetLooksAsync(CancellationToken cancellationToken = default);
    Task<LookbookLookDto> GetLookAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LookbookLookDto> CreateLookAsync(SaveLookbookLookRequestDto dto, CancellationToken cancellationToken = default);
    Task<LookbookLookDto> UpdateLookAsync(Guid id, SaveLookbookLookRequestDto dto, CancellationToken cancellationToken = default);
    Task DeleteLookAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FaqItemDto>> GetFaqItemsAsync(CancellationToken cancellationToken = default);
    Task<FaqItemDto> GetFaqItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FaqItemDto> CreateFaqItemAsync(SaveFaqItemRequestDto dto, CancellationToken cancellationToken = default);
    Task<FaqItemDto> UpdateFaqItemAsync(Guid id, SaveFaqItemRequestDto dto, CancellationToken cancellationToken = default);
    Task DeleteFaqItemAsync(Guid id, CancellationToken cancellationToken = default);
}
