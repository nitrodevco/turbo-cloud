using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Action;

/// <summary>
/// An ambassador warns a player; the target sees a moderator caution. Staff who moderate every
/// room may send it too, as the client offers the ambassador tools at security level 4 as well.
/// </summary>
[RequiresPermission(PermissionNodes.Role.AMBASSADOR, PermissionNodes.Room.MODERATE_ANY)]
public class AmbassadorAlertMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AmbassadorAlertMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AmbassadorAlertMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0 || message.UserId <= 0)
            return;

        await _grainFactory
            .SendComposerToPlayerAsync(
                message.UserId,
                new ModeratorCautionEventMessageComposer
                {
                    Message = AmbassadorAlertText.MESSAGE,
                    Url = string.Empty,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
