using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The user info window asks for a player's room visits (<c>RoomVisitsCtrl.show</c>).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class GetRoomVisitsMessageHandler(IModerationToolService moderation)
    : IMessageHandler<GetRoomVisitsMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        GetRoomVisitsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            await _moderation.GetRoomVisitsAsync(message.UserId, ct).ConfigureAwait(false)
            is not { } visits
        )
            return;

        await ctx.SendComposerAsync(
                new RoomVisitsEventMessageComposer
                {
                    UserId = message.UserId,
                    UserName = visits.UserName,
                    Rooms = visits.Rooms,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
