using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Khazar.WebAPI.HealthChecks;

/// <summary>
/// Writes a health report as camelCase JSON. Exception details are deliberately omitted:
/// /health is anonymous, so it must not reveal connection strings, server names or stack traces.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        // A cached "Healthy" response would hide an outage from load balancers and monitors.
        context.Response.Headers.CacheControl = "no-store, no-cache";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            timestampUtc = DateTime.UtcNow,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                    tags = entry.Value.Tags,
                    data = entry.Value.Data,
                }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions), context.RequestAborted);
    }
}
