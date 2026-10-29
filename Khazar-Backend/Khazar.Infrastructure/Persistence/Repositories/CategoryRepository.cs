using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository(KhazarDbContext dbContext)
    : BaseRepository<Category>(dbContext), ICategoryRepository
{
    public override async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ManagedCategory>> GetAllForAdminAsync(CancellationToken cancellationToken = default)
    {
        var categories = await GetAllAsync(cancellationToken);
        var productCounts = await DbContext.Products
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(row => row.CategoryId, row => row.Count, cancellationToken);

        return categories
            .Select(category => new ManagedCategory(category, productCounts.GetValueOrDefault(category.Id)))
            .ToList();
    }

    public async Task<ManagedCategory?> GetForAdminByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await GetByIdAsync(id, cancellationToken);
        if (category is null)
            return null;

        var productCount = await DbContext.Products.CountAsync(p => p.CategoryId == id, cancellationToken);
        return new ManagedCategory(category, productCount);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? exceptCategoryId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(c => c.Name == name && c.Id != exceptCategoryId, cancellationToken);

    public async Task<bool> SlugExistsAsync(string slug, Guid? exceptCategoryId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(c => c.Slug == slug && c.Id != exceptCategoryId, cancellationToken);

    public override async Task<Category> AddAsync(Category entity, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.AddAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (TranslateSaveFailure(ex) is { } conflict)
        {
            throw conflict;
        }
    }

    public override async Task UpdateAsync(Category entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await base.UpdateAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (TranslateSaveFailure(ex) is { } conflict)
        {
            throw conflict;
        }
    }

    public override async Task DeleteAsync(Category entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await base.DeleteAsync(entity, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsConstraintViolation(ProductConfiguration.CategoryForeignKeyName))
        {
            // The service checks the product count first; this covers a product created in the meantime (FK Restrict).
            throw new ConflictException(
                "This category still has products. Move them to another category first.", "category_has_products");
        }
    }

    // A concurrent create or rename can still hit the unique indexes after the service's own checks.
    private static ConflictException? TranslateSaveFailure(DbUpdateException exception)
    {
        if (exception.IsUniqueIndexViolation(CategoryConfiguration.NameIndexName))
            return new ConflictException("Another category already uses this name.", "category_name_taken");
        if (exception.IsUniqueIndexViolation(CategoryConfiguration.SlugIndexName))
            return new ConflictException("Another category already uses this slug.", "category_slug_taken");
        return null;
    }
}
