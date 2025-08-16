using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Behaviors;

public class ExceptionHandlingBehavior(ILogger logger) : IPacketBehavior
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
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled handler error {Header} sid={Sid}",
                env.Msg.Header,
                env.ChannelId
            );
        }
    }
}
