using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Rooms.Object.Logic.Plugins;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IHotelWired"/> over the room grains: an event goes to the room as a room event, and
/// a room no one is in (by the room directory) is not loaded for it.
/// </summary>
internal sealed class HostHotelWired(IGrainFactory grains, IHotelPlayers players) : IHotelWired
{
    public async Task<bool> FireAsync<TEvent>(
        RoomId room,
        TEvent evt,
        PlayerId? triggerer,
        CancellationToken ct
    )
        where TEvent : IWiredEvent
    {
        if (
            await grains
                .GetRoomDirectoryGrain()
                .GetRoomPopulationAsync(room, ct)
                .ConfigureAwait(false) <= 0
        )
            return false;

        await grains
            .GetRoomGrain(room)
            .PublishRoomEventAsync(PluginWiredEvents.FromHotel(room, evt, triggerer), ct)
            .ConfigureAwait(false);

        return true;
    }

    public async Task<bool> FireForPlayerAsync<TEvent>(
        PlayerId player,
        TEvent evt,
        CancellationToken ct
    )
        where TEvent : IWiredEvent =>
        await players.CurrentRoomAsync(player, ct).ConfigureAwait(false) is { } room
        && await FireAsync(room, evt, player, ct).ConfigureAwait(false);
}
