using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<RoomHoldOutcome> HoldAsync(RoomHold hold, CancellationToken ct)
    {
        var now = NowMs();
        var key = new RoomHoldKey(hold.Plugin, hold.Holder, hold.Watcher);

        DropExpiredHolds(now);

        if (
            !_state.Holds.ContainsKey(key)
            && _state.Holds.Count >= RoomWatchLimits.MAX_HOLDS_PER_ROOM
        )
            return Task.FromResult(RoomHoldOutcome.RoomFull);

        var lease = RoomWatchLimits.Clamp(System.TimeSpan.FromMilliseconds(hold.LeaseMs));

        _state.Holds[key] = now + (long)lease.TotalMilliseconds;

        // Awake from now: the empty room's count starts over once the last hold ends.
        _emptySinceMs = -1;
        DelayRoomDeactivation();
        WakeTick();

        return Task.FromResult(RoomHoldOutcome.Held);
    }

    public Task<bool> ReleaseHoldAsync(
        string plugin,
        string holder,
        PlayerId watcher,
        CancellationToken ct
    ) => Task.FromResult(_state.Holds.Remove(new RoomHoldKey(plugin, holder, watcher)));

    public Task ReleasePluginHoldsAsync(string plugin, CancellationToken ct)
    {
        foreach (var key in _state.Holds.Keys.Where(x => x.Plugin == plugin).ToList())
        {
            _state.Holds.Remove(key);

            // Told, not awaited: this runs in the room's turn, and the presence's watch ends
            // without calling the room back.
            if (key.Watcher > 0)
                _grainFactory
                    .GetPlayerPresenceGrain(key.Watcher)
                    .UnwatchRoomAsync(_state.RoomId, plugin, CancellationToken.None)
                    .LogAndForget(
                        _logger,
                        "end the watch of player {PlayerId} on room {RoomId}",
                        key.Watcher,
                        _state.RoomId
                    );
        }

        return Task.CompletedTask;
    }

    /// <summary>Whether a hold's lease still runs.</summary>
    private bool IsHeld(long now)
    {
        foreach (var expiresAt in _state.Holds.Values)
            if (expiresAt > now)
                return true;

        return false;
    }

    private void DropExpiredHolds(long now)
    {
        if (_state.Holds.Count == 0)
            return;

        foreach (var (key, expiresAt) in _state.Holds.ToList())
            if (expiresAt <= now)
                _state.Holds.Remove(key);
    }
}

/// <summary>Who a hold is for: a plugin's own (by its holder name) or a player's watch.</summary>
internal readonly record struct RoomHoldKey(string Plugin, string Holder, PlayerId Watcher);
