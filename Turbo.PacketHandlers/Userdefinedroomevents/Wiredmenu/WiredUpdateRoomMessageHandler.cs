using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// The wired menu's Reload and Rollback (and <c>:reload</c> / <c>:rollback</c>). The room writes
/// what it keeps and sends everyone out; each of them then has their room session closed and is
/// sent back in the ordinary way, so a doorbell or password asks again
/// (<c>wiredmenu.settings.room_state.reload.warning</c>: "Everyone will re-enter the room").
/// Closed here, not by the room, because the room cannot wait on a presence that may be waiting
/// on it, and the forward must follow the close.
/// </summary>
public class WiredUpdateRoomMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredUpdateRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredUpdateRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var roomId = ctx.RoomId;
        var playerIds = await _grainFactory
            .GetRoomGrain(roomId)
            .UpdateWiredRoomStateAsync(ctx.AsActionContext(), message.Rollback, ct)
            .ConfigureAwait(false);

        foreach (var playerId in playerIds)
        {
            await _grainFactory
                .GetPlayerPresenceGrain(playerId)
                .OnRemovedFromRoomAsync(roomId, false, ct)
                .ConfigureAwait(false);

            await _grainFactory
                .ForwardPlayerToRoomAsync(playerId, roomId, ct)
                .ConfigureAwait(false);
        }
    }
}
