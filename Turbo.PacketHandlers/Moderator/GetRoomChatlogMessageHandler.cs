using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The start panel and the room tool ask for a room's chatlog (<c>StartPanelCtrl.onChatlogButton</c>, <c>RoomToolCtrl.onChatlog</c>).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class GetRoomChatlogMessageHandler(IModerationToolService moderation)
    : IMessageHandler<GetRoomChatlogMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        GetRoomChatlogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var record = await _moderation
            .GetRoomChatlogAsync(message.RoomId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new RoomChatlogEventMessageComposer { Record = record }, ct)
            .ConfigureAwait(false);
    }
}
