using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Plugins;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IPluginRoomLeases"/> on this server: the rooms each plugin holds or has watched
/// from here, until when, so a plugin is held to its share of rooms and everything it has is let
/// go as it unloads (<see cref="IPluginStopping"/>). The rooms and the presences are where the
/// holds and watches live and run out; this only counts and remembers.
/// </summary>
internal sealed class PluginRoomLeases(
    IGrainFactory grains,
    TimeProvider time,
    ILogger<PluginRoomLeases> logger
) : IPluginRoomLeases, IPluginStopping
{
    private readonly Lock _gate = new();

    // Plugin -> room -> what it has there ("h:" holder, "w:" watcher) -> until when.
    private readonly Dictionary<
        string,
        Dictionary<RoomId, Dictionary<string, DateTimeOffset>>
    > _byPlugin = new(StringComparer.Ordinal);

    public async Task<RoomHoldOutcome> HoldAsync(
        string plugin,
        RoomId room,
        string holder,
        TimeSpan lease,
        CancellationToken ct
    )
    {
        lease = RoomWatchLimits.Clamp(lease);

        if (!HasRoomFor(plugin, room))
            return RoomHoldOutcome.PluginFull;

        var grain = grains.GetRoomGrain(room);
        RoomHoldOutcome outcome;

        try
        {
            await grain.EnsureRoomActiveAsync(ct).ConfigureAwait(false);
            outcome = await grain
                .HoldAsync(
                    new RoomHold
                    {
                        Plugin = plugin,
                        Holder = holder,
                        LeaseMs = (long)lease.TotalMilliseconds,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Plugin {Plugin} could not hold room {RoomId}", plugin, room);

            return RoomHoldOutcome.Failed;
        }

        if (outcome == RoomHoldOutcome.Held)
            Record(plugin, room, HoldKey(holder), lease);

        return outcome;
    }

    public async Task<bool> ReleaseAsync(
        string plugin,
        RoomId room,
        string holder,
        CancellationToken ct
    )
    {
        // Told to the room whatever was recorded here: the hold may have been taken on another server.
        Forget(plugin, room, HoldKey(holder));

        return await grains
            .GetRoomGrain(room)
            .ReleaseHoldAsync(plugin, holder, default, ct)
            .ConfigureAwait(false);
    }

    public async Task<RoomWatchOutcome> WatchAsync(
        string plugin,
        PlayerId player,
        RoomId room,
        PluginMessageComposer envelope,
        TimeSpan lease,
        bool throughLinkedPair,
        CancellationToken ct
    )
    {
        lease = RoomWatchLimits.Clamp(lease);

        if (!HasRoomFor(plugin, room))
            return RoomWatchOutcome.NotHeld;

        var outcome = await grains
            .GetPlayerPresenceGrain(player)
            .WatchRoomAsync(
                new RoomWatchRequest
                {
                    RoomId = room,
                    Plugin = plugin,
                    Envelope = envelope,
                    LeaseMs = (long)lease.TotalMilliseconds,
                    ThroughLinkedPair = throughLinkedPair,
                },
                ct
            )
            .ConfigureAwait(false);

        if (outcome == RoomWatchOutcome.Watching)
            Record(plugin, room, WatchKey(player), lease);
        else
            Forget(plugin, room, WatchKey(player));

        return outcome;
    }

    public async Task<bool> UnwatchAsync(
        string plugin,
        PlayerId player,
        RoomId room,
        CancellationToken ct
    )
    {
        Forget(plugin, room, WatchKey(player));

        return await grains
            .GetPlayerPresenceGrain(player)
            .UnwatchRoomAsync(room, plugin, ct)
            .ConfigureAwait(false);
    }

    public async Task ReleasePluginAsync(string plugin, CancellationToken ct)
    {
        List<RoomId> rooms;

        lock (_gate)
        {
            if (!_byPlugin.Remove(plugin, out var held))
                return;

            // A room whose leases all ran out has nothing of the plugin's left, and may have
            // unloaded: asking it would load it again.
            var now = time.GetUtcNow();

            rooms =
            [
                .. held.Where(x => x.Value.Values.Any(until => until > now)).Select(x => x.Key),
            ];
        }

        foreach (var room in rooms)
        {
            try
            {
                await grains
                    .GetRoomGrain(room)
                    .ReleasePluginHoldsAsync(plugin, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Could not release the holds of plugin {Plugin} on room {RoomId}",
                    plugin,
                    room
                );
            }
        }
    }

    public Task StoppingAsync(string pluginKey, CancellationToken ct) =>
        ReleasePluginAsync(pluginKey, ct);

    /// <summary>The rooms the plugin holds or has watched now, for tests.</summary>
    internal int RoomsOf(string plugin)
    {
        lock (_gate)
            return LiveRooms(plugin, time.GetUtcNow());
    }

    /// <summary>Whether the plugin has the room already, or room for one more.</summary>
    private bool HasRoomFor(string plugin, RoomId room)
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();

            if (
                _byPlugin.TryGetValue(plugin, out var rooms)
                && rooms.TryGetValue(room, out var there)
                && there.Values.Any(until => until > now)
            )
                return true;

            return LiveRooms(plugin, now) < RoomWatchLimits.MAX_ROOMS_PER_PLUGIN;
        }
    }

    // Under the gate. Drops what ran out as it counts.
    private int LiveRooms(string plugin, DateTimeOffset now)
    {
        if (!_byPlugin.TryGetValue(plugin, out var rooms))
            return 0;

        foreach (var (room, there) in rooms.ToList())
        {
            foreach (var (key, until) in there.ToList())
                if (until <= now)
                    there.Remove(key);

            if (there.Count == 0)
                rooms.Remove(room);
        }

        return rooms.Count;
    }

    private void Record(string plugin, RoomId room, string key, TimeSpan lease)
    {
        lock (_gate)
        {
            if (!_byPlugin.TryGetValue(plugin, out var rooms))
                _byPlugin[plugin] = rooms = [];

            if (!rooms.TryGetValue(room, out var there))
                rooms[room] = there = new(StringComparer.Ordinal);

            there[key] = time.GetUtcNow() + lease;
        }
    }

    private bool Forget(string plugin, RoomId room, string key)
    {
        lock (_gate)
        {
            if (
                !_byPlugin.TryGetValue(plugin, out var rooms)
                || !rooms.TryGetValue(room, out var there)
                || !there.Remove(key)
            )
                return false;

            if (there.Count == 0)
                rooms.Remove(room);

            return true;
        }
    }

    private static string HoldKey(string holder) => "h:" + holder;

    private static string WatchKey(PlayerId player) => "w:" + player.Value;
}
