using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>
/// Holds: leases that keep the room loaded and awake with no one in it, for a plugin drawing it
/// elsewhere or a player watching it (<c>docs/plugin-room-watch.md</c>). Reach them through
/// <c>IHotel.Rooms</c> or <c>IPluginRoomLeases</c>, which count each plugin's rooms.
/// </summary>
public partial interface IRoomGrain
{
    /// <summary>
    /// Takes or renews <paramref name="hold"/>. While any hold's lease runs, the room neither
    /// unloads nor goes dormant: its pets, bots, rollers and periodic wired run as with someone in
    /// it. Load the room first (<see cref="EnsureRoomActiveAsync"/>).
    /// </summary>
    public Task<RoomHoldOutcome> HoldAsync(RoomHold hold, CancellationToken ct);

    /// <summary>Ends a hold before its lease runs out; false when there was none.</summary>
    public Task<bool> ReleaseHoldAsync(
        string plugin,
        string holder,
        PlayerId watcher,
        CancellationToken ct
    );

    /// <summary>
    /// Ends every hold of <paramref name="plugin"/> (it is unloading), and the watches they
    /// were for: each watcher's presence is told.
    /// </summary>
    public Task ReleasePluginHoldsAsync(string plugin, CancellationToken ct);
}
