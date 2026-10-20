using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
    /// <summary>
    /// As Habbo does (official client capture 2026-10-16, both buttons): each player sent out is
    /// told they are not playing a game and has their room session closed, with no error or
    /// notification, and about two seconds later is forwarded back in the ordinary way, so a
    /// doorbell or password asks again. Closed here, not by the room, because the room cannot
    /// wait on a presence that may be waiting on it, and the forward must follow the close.
    /// </summary>
    public async Task ReloadWiredRoomAsync(ActionContext ctx, bool rollback, CancellationToken ct)
    {
        var roomId = ctx.RoomId;
        var playerIds = await _grainFactory
            .GetRoomGrain(roomId)
            .UpdateWiredRoomStateAsync(ctx, rollback, ct)
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
