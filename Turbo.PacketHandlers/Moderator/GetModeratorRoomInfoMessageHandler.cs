using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The room tool asks for a room's summary (<c>RoomToolCtrl.show</c>).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class GetModeratorRoomInfoMessageHandler(IModerationToolService moderation)
    : IMessageHandler<GetModeratorRoomInfoMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        GetModeratorRoomInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var room = await _moderation.GetRoomInfoAsync(message.RoomId, ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(new ModeratorRoomInfoEventMessageComposer { Room = room }, ct)
            .ConfigureAwait(false);
    }
}
