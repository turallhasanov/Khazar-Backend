using Khazar.Application.Common.Interfaces;
using Khazar.Infrastructure.ExternalServices;
using Microsoft.Extensions.Options;

namespace Khazar.WebAPI.BackgroundJobs;

/// <summary>
/// Re-queues orders left Pending (a failed push, or ids lost from the in-memory queue on restart) for
/// <see cref="OrderErpSyncWorker"/>. Pushing is idempotent on the order number in NexusERP, so a repeat is safe.
/// Attempts and back-off are tracked in memory: after a restart every Pending order gets a fresh set of attempts.
/// </summary>
public sealed class OrderErpSyncSweeper(
    IOrderErpSyncQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<NexusErpOptions> options,
    ILogger<OrderErpSyncSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    // Orders younger than this are still on their first push from checkout.
    private static readonly TimeSpan MinAge = TimeSpan.FromMinutes(1);

    private const int MaxAttempts = 10;
    private const int BatchSize = 50;

    private readonly Dictionary<Guid, (int Attempts, DateTime NextAttemptUtc)> _retries = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        try
        {
            // First round shortly after startup: orders left Pending by a previous run need not wait a full interval.
            await Task.Delay(StartupDelay, stoppingToken);

            do
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Sweeping Pending orders for NexusERP failed; trying again next round.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SyncEnabled)
            return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var now = DateTime.UtcNow;
        var pendingIds = await orderRepository.GetPendingIdsAsync(now - MinAge, BatchSize, cancellationToken);

        // Orders that synced or were cancelled no longer need their retry state.
        _retries.Keys.Where(id => !pendingIds.Contains(id)).ToList().ForEach(id => _retries.Remove(id));

        foreach (var id in pendingIds)
        {
            var (attempts, nextAttemptUtc) = _retries.GetValueOrDefault(id);

            if (attempts >= MaxAttempts || now < nextAttemptUtc)
                continue;

            attempts++;
            // 2, 4, 8 ... minutes, capped at an hour.
            var backoff = TimeSpan.FromMinutes(Math.Min(Math.Pow(2, attempts), 60));
            _retries[id] = (attempts, now + backoff);

            if (attempts == MaxAttempts)
                logger.LogError("Order {OrderId} is still Pending after {Attempts} NexusERP sync attempts; re-queue it manually.", id, attempts);

            queue.Enqueue(id);
        }
    }
}
