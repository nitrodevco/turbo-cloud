using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Primitives.Players.Grains;

/// <summary>
/// Watches: the player is sent what another room publishes (its avatars, furni, chat) beside
/// their own room's packets, each wrapped in a plugin's envelope, for a plugin drawing that room
/// (<c>docs/plugin-room-watch.md</c>). Flows: they await the watched room. Reach them through
/// <c>IHotel.Rooms</c> or <c>IPluginRoomLeases</c>, which count each plugin's rooms.
/// </summary>
public partial interface IPlayerPresenceGrain
{
    /// <summary>
    /// Starts or renews the player's watch of <see cref="RoomWatchRequest.RoomId"/>: only when
    /// they could walk into it (bans, and its door unless the watch is through a linked pair into
    /// it). A watch holds the room, and ends when the lease runs out, on
    /// <see cref="UnwatchRoomAsync"/>, when the session ends or when the plugin unloads. Entering
    /// the watched room ends it too: from then on the player gets the room's packets as anyone
    /// in it does.
    /// </summary>
    public Task<RoomWatchOutcome> WatchRoomAsync(RoomWatchRequest request, CancellationToken ct);

    /// <summary>
    /// Ends the player's watch of <paramref name="roomId"/> for <paramref name="plugin"/>, and
    /// its hold on the room; false when there was none. A room may tell it (never awaited).
    /// </summary>
    public Task<bool> UnwatchRoomAsync(RoomId roomId, string plugin, CancellationToken ct);
}
