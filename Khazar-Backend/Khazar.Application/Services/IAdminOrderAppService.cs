using Khazar.Application.DTOs.Orders;

namespace Khazar.Application.Services;

/// <summary>
/// Order management for the admin panel. Orders are never erased (they are the sales record): they are completed or
/// cancelled, and "deleting" an order cancels it, which removes it from the default list. A transition the order's status does not allow is a ConflictException ("invalid_order_status").
/// </summary>
public interface IAdminOrderAppService
{
    /// <summary>
    /// Active orders, newest first; <paramref name="status"/> (e.g. "Pending") filters them. Cancelled orders are
    /// left out unless <paramref name="status"/> is "Cancelled".
    /// </summary>
    Task<IReadOnlyList<OrderResponseDto>> GetAllAsync(string? status, CancellationToken cancellationToken = default);

    Task<OrderResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks an order that NexusERP accepted (SyncedToErp) as delivered.</summary>
    Task<OrderResponseDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cancels an order that is not completed and returns its items to stock.</summary>
    Task<OrderResponseDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the order with this number because NexusERP cancelled its sale: items return to stock and NexusERP is not
    /// called back. An already cancelled order succeeds unchanged; a completed one is a ConflictException.
    /// </summary>
    Task<OrderResponseDto> CancelFromErpAsync(string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>Queues a Pending order (e.g. one NexusERP rejected) for another NexusERP push.</summary>
    Task<OrderResponseDto> RetryErpSyncAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a Pending order to NexusERP right now and returns it as SyncedToErp. Throws ExternalServiceException
    /// when NexusERP does not accept it; the order then stays Pending.
    /// </summary>
    Task<OrderResponseDto> SyncToNexusAsync(Guid id, CancellationToken cancellationToken = default);
}
