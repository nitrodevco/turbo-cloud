using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>
/// The room tool's checkboxes (<c>RoomToolCtrl.as:363-366</c>): door to doorbell, the room renamed
/// as inappropriate, everyone kicked and the event ended. No reply: the tool closes on send.
/// </summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModerateRoomMessageHandler(IModerationToolService moderation)
    : IMessageHandler<ModerateRoomMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        ModerateRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _moderation
            .ModerateRoomAsync(
                ctx.PlayerId,
                message.RoomId,
                message.LockDoor,
                message.ChangeName,
                message.Kick,
                ct
            )
            .ConfigureAwait(false);
    }
}
