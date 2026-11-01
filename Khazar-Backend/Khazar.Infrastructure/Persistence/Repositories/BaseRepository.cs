using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the generic repository contract.
/// List queries are read-only (AsNoTracking). GetByIdAsync returns a tracked aggregate so that
/// changes made to it by the application layer are persisted by the next save.
/// </summary>
public abstract class BaseRepository<T>(KhazarDbContext dbContext) : IAsyncRepository<T>
    where T : BaseEntity
{
    protected KhazarDbContext DbContext { get; } = dbContext;

    protected DbSet<T> Set => DbContext.Set<T>();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().ToListAsync(cancellationToken);

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        // Tracked entities are already being change-tracked; only detached ones need to be attached.
        if (DbContext.Entry(entity).State == EntityState.Detached)
            Set.Update(entity);

        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        Set.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
