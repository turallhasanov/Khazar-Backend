using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

/// <summary>
/// Category persistence contract. GetAllAsync returns categories in storefront order (SortOrder, then Name).
/// AddAsync and UpdateAsync report a duplicate name or slug as a <see cref="Exceptions.ConflictException"/>.
/// </summary>
public interface ICategoryRepository : IAsyncRepository<Category>
{
    /// <summary>Every category in storefront order, with how many products (archived ones included) use it.</summary>
    Task<IReadOnlyList<ManagedCategory>> GetAllForAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>One category (tracked, for changes) with its product count, or null.</summary>
    Task<ManagedCategory?> GetForAdminByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? exceptCategoryId = null, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, Guid? exceptCategoryId = null, CancellationToken cancellationToken = default);
}

/// <summary>A category for the admin panel. One with products (<paramref name="ProductCount"/> &gt; 0) cannot be deleted.</summary>
public sealed record ManagedCategory(Category Category, int ProductCount);
