using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Networking.Pipeline;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Behaviors;

public class CleanupBehavior(IIngressPipeline ingress, ILogger logger) : IPacketBehavior
{
    public async Task InvokeAsync(
        IPacketContext ctx,
        PacketEnvelope env,
        CancellationToken ct,
        PacketDelegate next
    )
    {
        try
        {
            await next(ctx, env, ct).ConfigureAwait(false);
        }
        finally
        {
            env.Msg.Content.Release();

            ingress.OnProcessed(ctx.IngressToken);
        }
    }
}
