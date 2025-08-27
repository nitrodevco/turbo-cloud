using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;

namespace Turbo.Networking.Behaviors;

public class RevisionDispatchBehavior(
    IRevisionManager revisionManager,
    IPacketMessageHub messageHub,
    ILogger logger
) : IPacketBehavior
{
    public async Task InvokeAsync(
        IPacketContext ctx,
        PacketEnvelope env,
        CancellationToken ct,
        PacketDelegate next
    )
    {
        var revision = ctx.SessionContext.Revision;

        logger.LogDebug(env.Msg.Header.ToString());

        if (revision is not null)
        {
            if (revision.Parsers.TryGetValue(env.Msg.Header, out var parser))
            {
                await parser
                    .HandleAsync(ctx.SessionContext, env.Msg, messageHub, ct)
                    .ConfigureAwait(false);
            }
        }
    }
}
