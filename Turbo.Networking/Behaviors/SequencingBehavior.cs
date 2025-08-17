using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Behaviors;

public class SequencingBehavior(ILogger logger) : IPacketBehavior
{
    private readonly ConcurrentDictionary<IChannelId, SemaphoreSlim> _locks = new();

    public async Task InvokeAsync(
        IPacketContext ctx,
        PacketEnvelope env,
        CancellationToken ct,
        PacketDelegate next
    )
    {
        var semaphore = _locks.GetOrAdd(env.ChannelId, static _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await next(ctx, env, ct).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
