using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

// Content persistence contracts. GetAllAsync returns rows in display order (SortOrder, then creation time) for
// articles, looks and FAQ items, and newest first for contact messages. GetByIdAsync returns a tracked entity.

public interface IContentSectionRepository : IAsyncRepository<ContentSection>
{
    /// <summary>The tracked section with this key, or null when it was never saved.</summary>
    Task<ContentSection?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>AddAsync and UpdateAsync report a duplicate slug as a ConflictException ("article_slug_taken").</summary>
public interface IJournalArticleRepository : IAsyncRepository<JournalArticle>
{
    Task<IReadOnlyList<JournalArticle>> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>The published article with this slug (lower-case), or null.</summary>
    Task<JournalArticle?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, Guid? exceptArticleId = null, CancellationToken cancellationToken = default);
}

public interface ILookbookLookRepository : IAsyncRepository<LookbookLook>
{
    Task<IReadOnlyList<LookbookLook>> GetPublishedAsync(CancellationToken cancellationToken = default);
}

public interface IFaqItemRepository : IAsyncRepository<FaqItem>
{
    Task<IReadOnlyList<FaqItem>> GetPublishedAsync(CancellationToken cancellationToken = default);
}

public interface IContactMessageRepository : IAsyncRepository<ContactMessage>;
