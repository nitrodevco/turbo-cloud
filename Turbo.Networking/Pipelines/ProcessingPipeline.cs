using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Networking.Pipeline;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Pipelines;

public class ProcessingPipeline : IProcessingPipeline
{
    private readonly PacketDelegate _app;

    public ProcessingPipeline(IEnumerable<IPacketBehavior> behaviors)
    {
        // terminal = no-op (RevisionDispatchBehavior is the “real” handler)
        PacketDelegate terminal = (ctx, env, ct) => Task.CompletedTask;

        _app = behaviors
            .Reverse()
            .Aggregate(
                terminal,
                (next, behavior) => (ctx, env, ct) => behavior.InvokeAsync(ctx, env, ct, next)
            );
    }

    public Task ProcessAsync(IPacketContext ctx, PacketEnvelope env, CancellationToken ct) =>
        _app(ctx, env, ct);
}
