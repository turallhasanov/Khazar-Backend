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

    /// <summary>
    /// Sends the order to NexusERP, creating any product it does not know yet. Succeeds only when NexusERP accepts
    /// the order (HTTP 200/201); otherwise the result carries the exact reason.
    /// </summary>
    Task<ErpPushResult> PushOrderToErpAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the sale NexusERP created for this order (DELETE api/online-orders/{orderNumber}); NexusERP returns the
    /// items to its stock. Succeeds when NexusERP cancelled it, had already cancelled it, or never knew the order.
    /// </summary>
    Task<ErpPushResult> CancelOrderInErpAsync(string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active NexusERP products with their live stock (summed over the configured warehouse, or all warehouses).
    /// Returns null when NexusERP is unreachable or answers with an error.
    /// </summary>
    Task<IReadOnlyList<NexusProductSnapshot>?> GetProductsAsync(CancellationToken cancellationToken = default);
}

/// <summary>A NexusERP product as the catalog pull sees it. <paramref name="Sku"/> is its barcode, upper-case.</summary>
public sealed record NexusProductSnapshot(string Sku, string Name, string? CategoryName, decimal SalePrice, int Stock);

/// <summary>Outcome of an order push: <see cref="Error"/> is a user-facing reason when it failed.</summary>
public sealed record ErpPushResult(bool Success, string? Error)
{
    public static readonly ErpPushResult Ok = new(true, null);

    public static ErpPushResult Failed(string error) => new(false, error);
}
