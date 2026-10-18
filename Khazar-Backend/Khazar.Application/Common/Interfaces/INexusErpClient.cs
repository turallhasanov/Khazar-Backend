using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

/// <summary>
/// Integration contract for the NexusERP system. Implementations never throw on transport failures:
/// an unreachable or failing ERP is reported through the return value so callers can fall back.
/// Cancellation requested by the caller is still propagated as <see cref="OperationCanceledException"/>.
/// </summary>
public interface INexusErpClient
{
    /// <summary>Returns the live stock for the SKU, or null when NexusERP is unreachable or does not know the SKU.</summary>
    Task<int?> GetStockBySkuAsync(string sku, CancellationToken cancellationToken = default);

    /// <summary>Sends the order to NexusERP. Returns true only when NexusERP accepts it (HTTP 200/201).</summary>
    Task<bool> PushOrderToErpAsync(Order order, CancellationToken cancellationToken = default);
}
