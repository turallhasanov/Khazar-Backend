using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

// Variants and gallery images are always loaded in SortOrder: SQL Server does not guarantee row order otherwise,
// and the storefront relies on the first color being the default selection.
public sealed class ProductRepository(KhazarDbContext dbContext)
    : BaseRepository<Product>(dbContext), IProductRepository
{
    public override async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Tracked on purpose: checkout loads products through this method and reduces variant stock,
    /// which IOrderRepository.CreateAsync then commits together with the order. Archived products are returned too.
    /// </summary>
    public override async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Set
            .Include(p => p.Category)
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive, cancellationToken);

    // Matches the category's Name (the API key, e.g. "Coat"); the comparison relies on the database collation
    // (case-insensitive by default on SQL Server).
    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Where(p => p.Category.Name == category && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByVariantSkusAsync(
        IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default) =>
        await Set
            .Include(p => p.Category)
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Where(p => p.Variants.Any(v => skus.Contains(v.SKU)))
            .ToListAsync(cancellationToken);

    public async Task<Product?> GetActiveByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var wanted = name.Trim().ToLower();

        return await Set
            .Include(p => p.Category)
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Where(p => p.IsActive && p.Name.ToLower() == wanted)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManagedProduct>> GetAllForAdminAsync(CancellationToken cancellationToken = default)
    {
        var products = await Set
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var reviewCounts = await DbContext.ProductReviews
            .GroupBy(r => r.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(row => row.ProductId, row => row.Count, cancellationToken);

        var ordered = await DbContext.OrderItems
            .Select(i => new { i.ProductId, i.VariantId })
            .Distinct()
            .ToListAsync(cancellationToken);
        var orderedProducts = ordered.Select(row => row.ProductId).ToHashSet();
        var orderedVariants = ordered.Select(row => row.VariantId).ToHashSet();

        return products
            .Select(product => new ManagedProduct(
                product,
                reviewCounts.GetValueOrDefault(product.Id),
                orderedProducts.Contains(product.Id),
                product.Variants.Where(v => orderedVariants.Contains(v.Id)).Select(v => v.Id).ToHashSet()))
            .ToList();
    }

    public async Task<ManagedProduct?> GetForAdminByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await GetByIdAsync(id, cancellationToken);
        if (product is null)
            return null;

        var reviewCount = await DbContext.ProductReviews.CountAsync(r => r.ProductId == id, cancellationToken);
        var orderedVariantIds = await DbContext.OrderItems
            .Where(i => i.ProductId == id)
            .Select(i => i.VariantId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var hasOrders = orderedVariantIds.Count > 0;

        return new ManagedProduct(product, reviewCount, hasOrders, orderedVariantIds.ToHashSet());
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? exceptProductId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(p => p.Slug == slug && p.Id != exceptProductId, cancellationToken);

    public async Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default) =>
        await DbContext.ProductVariants.AnyAsync(v => v.SKU == sku, cancellationToken);

    public override async Task<Product> AddAsync(Product entity, CancellationToken cancellationToken = default)
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

    public override async Task UpdateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        // Variants added to a tracked product are new rows; their ids come from the domain, so mark them explicitly.
        foreach (var variant in entity.Variants.Where(v => DbContext.Entry(v).State == EntityState.Detached))
            DbContext.Entry(variant).State = EntityState.Added;

        try
        {
            await base.UpdateAsync(entity, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // StockQuantity is a concurrency token: a checkout changed the stock after the admin loaded it.
            throw new ConflictException(
                "The stock changed while you were editing it (an order was placed). Reload and try again.", "stock_changed");
        }
        catch (DbUpdateException ex) when (TranslateSaveFailure(ex) is { } conflict)
        {
            throw conflict;
        }
    }

    // A concurrent create or rename can still hit the unique indexes after the service's own checks.
    private static ConflictException? TranslateSaveFailure(DbUpdateException exception)
    {
        if (exception.IsUniqueIndexViolation(ProductConfiguration.SlugIndexName))
            return new ConflictException("Another product already uses this slug.", "slug_taken");
        if (exception.IsUniqueIndexViolation(ProductVariantConfiguration.SkuIndexName))
            return new ConflictException("Another variant already uses this SKU.", "sku_taken");
        if (exception.IsUniqueIndexViolation(ProductVariantConfiguration.ColorSizeIndexName))
            return new ConflictException("This product already has a variant with this color and size.", "variant_exists");
        return null;
    }
}
