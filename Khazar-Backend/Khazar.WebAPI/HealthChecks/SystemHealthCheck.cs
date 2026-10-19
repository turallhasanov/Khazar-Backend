using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Khazar.WebAPI.HealthChecks;

/// <summary>Settings bound from the "HealthChecks" configuration section.</summary>
public sealed class SystemHealthCheckOptions
{
    public const string SectionName = "HealthChecks";

    /// <summary>Process working set above which the API reports itself as Degraded.</summary>
    public int MemoryThresholdMb { get; set; } = 1024;
}

/// <summary>
/// Liveness check for the API process itself: reports uptime and memory usage, and turns Degraded when
/// the working set exceeds the configured threshold. It has no external dependencies, so it stays Healthy
/// whenever the process can serve requests.
/// </summary>
public sealed class SystemHealthCheck(IOptions<SystemHealthCheckOptions> options) : IHealthCheck
{
    private const double BytesPerMegabyte = 1024d * 1024d;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.GetCurrentProcess();

            var workingSetMb = process.WorkingSet64 / BytesPerMegabyte;
            var thresholdMb = options.Value.MemoryThresholdMb;

            var data = new Dictionary<string, object>
            {
                ["uptimeSeconds"] = (long)(DateTime.Now - process.StartTime).TotalSeconds,
                ["workingSetMb"] = Math.Round(workingSetMb, 1),
                ["managedHeapMb"] = Math.Round(GC.GetTotalMemory(forceFullCollection: false) / BytesPerMegabyte, 1),
                ["memoryThresholdMb"] = thresholdMb,
                ["processorCount"] = Environment.ProcessorCount,
            };

            var result = workingSetMb > thresholdMb
                ? HealthCheckResult.Degraded($"Working set exceeds {thresholdMb} MB.", data: data)
                : HealthCheckResult.Healthy("API process is running.", data);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            // Process metrics can be restricted in some hosting sandboxes; that is not a reason to report the API as down.
            return Task.FromResult(HealthCheckResult.Degraded("Process metrics are unavailable.", ex));
        }
    }
}
