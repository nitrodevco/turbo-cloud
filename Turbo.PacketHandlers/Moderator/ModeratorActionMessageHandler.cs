using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>
/// The room tool's caution or message to everyone in the room (<c>RoomToolCtrl.as:354-366</c>),
/// with a kick for actions 1 and 4. The packet names no room: the tool enables its send buttons
/// only in the room the moderator is in (<c>RoomToolCtrl.as:165-177</c>), so that is the room.
/// No reply: the tool closes on send.
/// </summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModeratorActionMessageHandler(IModerationToolService moderation)
    : IMessageHandler<ModeratorActionMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        ModeratorActionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        await _moderation
            .MessageRoomAsync(ctx.PlayerId, ctx.RoomId, message.ActionType, message.Message, ct)
            .ConfigureAwait(false);
    }
}
