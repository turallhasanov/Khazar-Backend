using System.Diagnostics;
using Khazar.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Khazar.WebAPI.HealthChecks;

/// <summary>
/// Verifies that SQL Server is reachable through the application's own DbContext and connection string.
/// The probe has its own timeout so a hung database cannot hold the health endpoint open.
/// </summary>
public sealed class DatabaseHealthCheck(KhazarDbContext dbContext, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // CanConnectAsync returns false (rather than throwing) for some failures, e.g. a missing database.
            var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
            stopwatch.Stop();

            var data = new Dictionary<string, object> { ["latencyMs"] = stopwatch.ElapsedMilliseconds };

            if (canConnect)
                return HealthCheckResult.Healthy("SQL Server is reachable.", data);

            logger.LogWarning("Database health check failed: SQL Server refused the connection or the database does not exist.");
            return new HealthCheckResult(context.Registration.FailureStatus, "SQL Server is not reachable.", data: data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Database health check timed out after {TimeoutSeconds} seconds.", ProbeTimeout.TotalSeconds);
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"SQL Server did not respond within {ProbeTimeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The exception is attached for logging only; the response writer never exposes it to clients.
            logger.LogWarning(ex, "Database health check failed with {ExceptionType}.", ex.GetType().Name);
            return new HealthCheckResult(context.Registration.FailureStatus, "SQL Server is not reachable.", ex);
        }
    }
}
