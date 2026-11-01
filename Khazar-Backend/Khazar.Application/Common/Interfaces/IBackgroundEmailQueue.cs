namespace Khazar.Application.Common.Interfaces;

/// <summary>
/// Sends email outside the request that triggered it, so a slow or failing mail server never delays or fails
/// the main operation (subscription, checkout, password reset). Failed deliveries are retried a few times and
/// then logged; they are never reported back to the caller.
/// </summary>
public interface IBackgroundEmailQueue
{
    /// <param name="description">Identifies the email in logs, e.g. "order confirmation KHZ-AB123".</param>
    /// <param name="send">
    /// Sends the email and returns whether it was handed off. Captures everything it needs at enqueue time:
    /// the request scope (DbContext, repositories) is gone when it runs.
    /// </param>
    void Enqueue(string description, Func<IEmailService, CancellationToken, Task<bool>> send);
}
