using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

// Variants are always loaded in SortOrder: SQL Server does not guarantee row order otherwise,
// and the storefront relies on the first color being the default selection.
public sealed class ProductRepository(KhazarDbContext dbContext)
    : BaseRepository<Product>(dbContext), IProductRepository
{
    public override async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Tracked on purpose: checkout loads products through this method and reduces variant stock,
    /// which IOrderRepository.CreateAsync then commits together with the order.
    /// </summary>
    public override async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Set
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);

    // Category comparison relies on the database collation (case-insensitive by default on SQL Server).
    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Include(p => p.Variants.OrderBy(v => v.SortOrder))
            .Where(p => p.Category == category)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
}
