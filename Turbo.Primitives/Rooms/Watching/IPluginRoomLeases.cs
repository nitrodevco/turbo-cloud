using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;

namespace Turbo.Primitives.Rooms.Watching;

/// <summary>
/// The one way plugins hold rooms and have players watch them (<c>IHotel.Rooms</c> and
/// <c>IFurniContext.WatchRoomAsync</c> come here): it counts each plugin's rooms on this server
/// against <see cref="RoomWatchLimits.MAX_ROOMS_PER_PLUGIN"/>, and ends all of a plugin's holds
/// and watches when the plugin unloads. Leases are clamped by <see cref="RoomWatchLimits.Clamp"/>.
/// </summary>
public interface IPluginRoomLeases
{
    /// <summary>Loads the room if it is not loaded and takes or renews the plugin's hold on it.</summary>
    Task<RoomHoldOutcome> HoldAsync(
        string plugin,
        RoomId room,
        string holder,
        TimeSpan lease,
        CancellationToken ct
    );

    /// <summary>Ends the plugin's hold; false when it had none of that name there.</summary>
    Task<bool> ReleaseAsync(string plugin, RoomId room, string holder, CancellationToken ct);

    /// <summary>
    /// Starts or renews <paramref name="player"/>'s watch of <paramref name="room"/>, each of its
    /// packets inside <paramref name="envelope"/>. <paramref name="throughLinkedPair"/> is core's
    /// own check (a linked pair's half whose other half stands in the room), never a plugin's.
    /// </summary>
    Task<RoomWatchOutcome> WatchAsync(
        string plugin,
        PlayerId player,
        RoomId room,
        PluginMessageComposer envelope,
        TimeSpan lease,
        bool throughLinkedPair,
        CancellationToken ct
    );

    /// <summary>Ends the player's watch of the room for the plugin; false when there was none.</summary>
    Task<bool> UnwatchAsync(string plugin, PlayerId player, RoomId room, CancellationToken ct);

    /// <summary>Ends every hold and watch the plugin has from this server (it is unloading).</summary>
    Task ReleasePluginAsync(string plugin, CancellationToken ct);
}
