using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>
/// A moderation tool packet. The tool is not built yet, so nothing is done with it; the gate is
/// in place so that when it is, only players who hold the tool reach it.
/// </summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModAlertMessageHandler : IMessageHandler<ModAlertMessage>
{
    public async ValueTask HandleAsync(
        ModAlertMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
