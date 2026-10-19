using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Enums;

namespace Khazar.WebAPI.BackgroundJobs;

/// <summary>
/// Pushes placed orders to NexusERP one at a time, outside the checkout request. Each order gets its own DI scope
/// (DbContext, repositories, ERP client), because the request scope that created it is already disposed.
/// An accepted order moves Pending → Processing → SyncedToErp; a rejected one stays Pending and is logged.
/// </summary>
public sealed class OrderErpSyncWorker(
    OrderErpSyncQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderErpSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var orderId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await SyncAsync(orderId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    // One failing order (e.g. NexusERP settings missing, database down) must not stop the worker.
                    logger.LogError(ex, "Order {OrderId} could not be synchronized with NexusERP; it stays Pending.", orderId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task SyncAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var erpClient = scope.ServiceProvider.GetRequiredService<INexusErpClient>();

        // Tracked and loaded with items, as PushOrderToErpAsync needs them.
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Order {OrderId} was queued for NexusERP but no longer exists.", orderId);
            return;
        }

        if (order.Status != OrderStatus.Pending)
            return;

        // The client logs the reason of a rejection or transport failure itself.
        if (!await erpClient.PushOrderToErpAsync(order, cancellationToken))
        {
            logger.LogWarning("Order {OrderNumber} was not accepted by NexusERP; it stays Pending.", order.OrderNumber);
            return;
        }

        order.StartProcessing();
        order.MarkSyncedToErp();
        await orderRepository.UpdateAsync(order, cancellationToken);
    }
}
