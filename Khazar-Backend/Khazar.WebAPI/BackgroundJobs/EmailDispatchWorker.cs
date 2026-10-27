using Khazar.Application.Common.Interfaces;

namespace Khazar.WebAPI.BackgroundJobs;

/// <summary>
/// Sends queued emails one at a time, outside the request that queued them. A failed delivery (mail server down,
/// rejected credentials) is retried with growing delays and then given up with an error log; it never reaches the
/// customer. Retries wait off the queue, so one failing email does not hold up the others.
/// </summary>
public sealed class EmailDispatchWorker(
    BackgroundEmailQueue queue,
    IEmailService emailService,
    ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    // Delay before attempt 2 and attempt 3; after the last one the email is dropped.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
            {
                bool sent;
                try
                {
                    sent = await job.Send(emailService, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    // IEmailService does not throw on delivery failures; this is a bug in the job (e.g. a template).
                    logger.LogError(ex, "Email '{Description}' failed with an unexpected error; it is not retried.", job.Description);
                    continue;
                }

                if (!sent)
                    ScheduleRetry(job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private void ScheduleRetry(EmailJob job, CancellationToken stoppingToken)
    {
        if (job.Attempt > RetryDelays.Length)
        {
            logger.LogError("Email '{Description}' could not be delivered after {Attempts} attempts; giving up.",
                job.Description, job.Attempt);
            return;
        }

        var delay = RetryDelays[job.Attempt - 1];
        logger.LogWarning("Email '{Description}' was not delivered (attempt {Attempt}); retrying in {Delay}.",
            job.Description, job.Attempt, delay);

        _ = RequeueAfterAsync(job with { Attempt = job.Attempt + 1 }, delay, stoppingToken);
    }

    private async Task RequeueAfterAsync(EmailJob job, TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
            queue.Requeue(job);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Email '{Description}' was pending a retry at shutdown and is dropped.", job.Description);
        }
    }
}
