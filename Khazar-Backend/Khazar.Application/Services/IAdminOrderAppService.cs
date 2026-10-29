using Khazar.Application.DTOs.Orders;

namespace Khazar.Application.Services;

/// <summary>
/// Order management for the admin panel. Orders are never deleted (they are the sales record); they are completed or
/// cancelled. A transition the order's status does not allow is a ConflictException ("invalid_order_status").
/// </summary>
public interface IAdminOrderAppService
{
    /// <summary>Every order, newest first; <paramref name="status"/> (e.g. "Pending") filters them.</summary>
    Task<IReadOnlyList<OrderResponseDto>> GetAllAsync(string? status, CancellationToken cancellationToken = default);

    Task<OrderResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks an order that NexusERP accepted (SyncedToErp) as delivered.</summary>
    Task<OrderResponseDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cancels an order that is not completed and returns its items to stock.</summary>
    Task<OrderResponseDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Queues a Pending order (e.g. one NexusERP rejected) for another NexusERP push.</summary>
    Task<OrderResponseDto> RetryErpSyncAsync(Guid id, CancellationToken cancellationToken = default);
}
