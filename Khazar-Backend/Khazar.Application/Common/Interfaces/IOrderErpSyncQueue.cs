namespace Khazar.Application.Common.Interfaces;

/// <summary>
/// Hands placed orders to a background worker that pushes them to NexusERP, so checkout never waits on the ERP.
/// </summary>
public interface IOrderErpSyncQueue
{
    void Enqueue(Guid orderId);
}
