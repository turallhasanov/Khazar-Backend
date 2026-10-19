using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(KhazarDbContext dbContext)
    : BaseRepository<Order>(dbContext), IOrderRepository
{
    // A collision on a 5-character code from a 32-symbol alphabet is rare; a few retries are plenty.
    private const int MaxOrderNumberAttempts = 5;

    private const string OrderNumberIndexName = "IX_Orders_OrderNumber";

    public override async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>Tracked so that status transitions (e.g. StartProcessing, Cancel) can be saved via UpdateAsync.</summary>
    public override async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Set
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default) =>
        await Set
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, cancellationToken);

    /// <summary>
    /// Saves the order with its items. The single SaveChanges call also commits stock changes on
    /// tracked variants, so the order and the stock reservation succeed or fail together.
    /// </summary>
    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(order, cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await DbContext.SaveChangesAsync(cancellationToken);
                return order;
            }
            catch (DbUpdateException ex) when (attempt < MaxOrderNumberAttempts && ex.IsUniqueIndexViolation(OrderNumberIndexName))
            {
                // A failed save leaves the change tracker untouched, so only the number needs to change.
                order.RegenerateOrderNumber();
            }
        }
    }
}
