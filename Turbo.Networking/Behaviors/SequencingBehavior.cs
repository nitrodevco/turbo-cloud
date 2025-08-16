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
    private readonly ConcurrentDictionary<IChannelId, Task> _tails = new();

    public Task InvokeAsync(
        IPacketContext ctx,
        PacketEnvelope env,
        CancellationToken ct,
        PacketDelegate next
    )
    {
        Task Run() => next(ctx, env, ct);

        var chained = _tails.AddOrUpdate(
            env.ChannelId,
            _ => Run(),
            (_, tail) =>
                tail.IsCompleted
                    ? Run()
                    : tail.ContinueWith(
                            _ => Run(),
                            ct,
                            TaskContinuationOptions.None,
                            TaskScheduler.Default
                        )
                        .Unwrap()
        );

        // swallow the scheduler task; return the scheduled chain task to the pipeline
        return chained.ContinueWith(
            t =>
            {
                if (t.IsFaulted)
                    throw t.Exception!; // let ExceptionHandlingBehavior see it
            },
            TaskScheduler.Default
        );
    }
}
