using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>
/// A moderation tool packet. The tool is not built yet, so nothing is done with it; the gate is
/// in place so that when it is, only players who hold the tool reach it.
/// </summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class PickIssuesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PickIssuesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PickIssuesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || !await _grainFactory
                .HasPermissionAsync(ctx.PlayerId, PermissionNodes.Moderation.TOOL, ct)
                .ConfigureAwait(false)
        )
            return;
    }
}
