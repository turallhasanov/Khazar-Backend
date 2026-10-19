using System.Threading.Channels;
using Khazar.Application.Common.Interfaces;

namespace Khazar.WebAPI.BackgroundJobs;

/// <summary>
/// In-memory queue between checkout and <see cref="OrderErpSyncWorker"/>. Queued ids are lost on restart; such
/// orders stay in Pending status, which identifies them for a later re-sync.
/// </summary>
public sealed class OrderErpSyncQueue : IOrderErpSyncQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    public ChannelReader<Guid> Reader => _channel.Reader;

    // An unbounded channel accepts every write until it is completed, and it is never completed.
    public void Enqueue(Guid orderId) => _channel.Writer.TryWrite(orderId);
}
