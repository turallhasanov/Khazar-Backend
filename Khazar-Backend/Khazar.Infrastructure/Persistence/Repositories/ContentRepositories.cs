using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

public sealed class ContentSectionRepository(KhazarDbContext dbContext)
    : BaseRepository<ContentSection>(dbContext), IContentSectionRepository
{
    public async Task<ContentSection?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

    /// <summary>Two admins saving a never-saved section at once: the second save replaces the first one's data.</summary>
    public override async Task<ContentSection> AddAsync(ContentSection entity, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.AddAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueIndexViolation(ContentSectionConfiguration.KeyIndexName))
        {
            DbContext.Entry(entity).State = EntityState.Detached;
            var existing = await GetByKeyAsync(entity.Key, cancellationToken)
                ?? throw new ConflictException("The section was changed at the same time; try again.");
            existing.Replace(entity.Data);
            await DbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }
    }
}

public sealed class JournalArticleRepository(KhazarDbContext dbContext)
    : BaseRepository<JournalArticle>(dbContext), IJournalArticleRepository
{
    public override async Task<IReadOnlyList<JournalArticle>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().OrderBy(a => a.SortOrder).ThenBy(a => a.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JournalArticle>> GetPublishedAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(a => a.IsPublished)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<JournalArticle?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.IsPublished, cancellationToken);

    public async Task<bool> SlugExistsAsync(string slug, Guid? exceptArticleId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(a => a.Slug == slug && a.Id != exceptArticleId, cancellationToken);

    public override async Task<JournalArticle> AddAsync(JournalArticle entity, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.AddAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueIndexViolation(JournalArticleConfiguration.SlugIndexName))
        {
            throw SlugTaken();
        }
    }

    public override async Task UpdateAsync(JournalArticle entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await base.UpdateAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueIndexViolation(JournalArticleConfiguration.SlugIndexName))
        {
            throw SlugTaken();
        }
    }

    // A concurrent save can still hit the unique index after the service's own check.
    private static ConflictException SlugTaken() => new("Another article already uses this slug.", "article_slug_taken");
}

public sealed class LookbookLookRepository(KhazarDbContext dbContext)
    : BaseRepository<LookbookLook>(dbContext), ILookbookLookRepository
{
    public override async Task<IReadOnlyList<LookbookLook>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookbookLook>> GetPublishedAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(l => l.IsPublished)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
}

public sealed class FaqItemRepository(KhazarDbContext dbContext)
    : BaseRepository<FaqItem>(dbContext), IFaqItemRepository
{
    public override async Task<IReadOnlyList<FaqItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FaqItem>> GetPublishedAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(f => f.IsPublished)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
}

public sealed class ContactMessageRepository(KhazarDbContext dbContext)
    : BaseRepository<ContactMessage>(dbContext), IContactMessageRepository
{
    public override async Task<IReadOnlyList<ContactMessage>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
}
