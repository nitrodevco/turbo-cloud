using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;
using Turbo.Rooms.Configuration;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// The wired menu's Reload and Rollback (and <c>:reload</c> / <c>:rollback</c>). The room writes
/// what it keeps and sends everyone out. As Habbo does (official client capture 2026-10-16, both
/// buttons): each of them is told they are not playing a game and has their room session closed,
/// with no error or notification, and about two seconds later is forwarded back in the ordinary
/// way, so a doorbell or password asks again. Closed here, not by the room, because the room
/// cannot wait on a presence that may be waiting on it, and the forward must follow the close.
/// </summary>
public class WiredUpdateRoomMessageHandler(
    IGrainFactory grainFactory,
    IOptions<RoomConfig> roomConfig,
    TimeProvider time
) : IMessageHandler<WiredUpdateRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly TimeProvider _time = time;

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

        if (playerIds.IsDefaultOrEmpty)
            return;

        foreach (var playerId in playerIds)
        {
            await _grainFactory
                .SendComposerToPlayerAsync(
                    playerId,
                    new YouArePlayingGameMessageComposer { IsPlaying = false },
                    ct
                )
                .ConfigureAwait(false);

            await _grainFactory
                .GetPlayerPresenceGrain(playerId)
                .OnRemovedFromRoomAsync(roomId, false, ct)
                .ConfigureAwait(false);
        }

        if (_roomConfig.RoomStateReturnDelayMs > 0)
            await Task.Delay(
                    TimeSpan.FromMilliseconds(_roomConfig.RoomStateReturnDelayMs),
                    _time,
                    ct
                )
                .ConfigureAwait(false);

        foreach (var playerId in playerIds)
            await _grainFactory
                .ForwardPlayerToRoomAsync(playerId, roomId, ct)
                .ConfigureAwait(false);
    }
}
