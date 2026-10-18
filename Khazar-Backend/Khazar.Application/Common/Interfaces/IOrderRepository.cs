using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

/// <summary>Order persistence contract. GetByIdAsync is inherited from <see cref="IAsyncRepository{T}"/>.</summary>
public interface IOrderRepository : IAsyncRepository<Order>
{
    /// <summary>
    /// Persists a new order. On an order number unique-index collision, implementations must call
    /// <see cref="Order.RegenerateOrderNumber"/> and retry instead of surfacing the conflict.
    /// </summary>
    /// <remarks>
    /// Stock changes made on the ordered variants before this call must be committed in the same
    /// transaction as the order, so that a failed save never leaves stock reserved without an order.
    /// </remarks>
    Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Finds an order by its public number (KHZ-XXXXX format).</summary>
    Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
}
