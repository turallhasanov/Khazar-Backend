using System.Threading.Channels;
using Khazar.Application.Common.Interfaces;

namespace Khazar.WebAPI.BackgroundJobs;

/// <summary>
/// In-memory queue between the use cases and <see cref="EmailDispatchWorker"/>. Queued emails are lost on restart;
/// the operation that queued them (subscription, order, reset request) is already committed and stays valid.
/// </summary>
public sealed class BackgroundEmailQueue : IBackgroundEmailQueue
{
    private readonly Channel<EmailJob> _channel = Channel.CreateUnbounded<EmailJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    public ChannelReader<EmailJob> Reader => _channel.Reader;

    public void Enqueue(string description, Func<IEmailService, CancellationToken, Task<bool>> send)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(send);
        Requeue(new EmailJob(description, send, Attempt: 1));
    }

    // An unbounded channel accepts every write until it is completed, and it is never completed.
    internal void Requeue(EmailJob job) => _channel.Writer.TryWrite(job);
}

/// <summary>A queued email.</summary>
/// <param name="Description">Identifies the email in logs.</param>
/// <param name="Send">Sends the email; returns false when delivery failed and may be retried.</param>
/// <param name="Attempt">1 for the first delivery attempt, incremented on each retry.</param>
public sealed record EmailJob(string Description, Func<IEmailService, CancellationToken, Task<bool>> Send, int Attempt);
