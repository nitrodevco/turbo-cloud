using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Players.Grains;

/// <summary>
/// Watches: another room's stream, besides the active room's, whose packets reach the session in
/// a plugin's envelope (<see cref="PluginEnvelopeComposer"/>), queued in order with everything
/// else the player is sent. The room's packets are the same instances its players get, so each
/// is still serialized once for everyone; only the envelope's frame is per player. A watch
/// starts with the room as an entry draws it (<c>IRoomGrain.GetWatchViewAsync</c>), before any of
/// its publishes.
/// </summary>
internal sealed partial class PlayerPresenceGrain
{
    public async Task<RoomWatchOutcome> WatchRoomAsync(
        RoomWatchRequest request,
        CancellationToken ct
    )
    {
        var roomId = request.RoomId;

        if (_sessionObserver is null)
            return RoomWatchOutcome.NotOnline;

        if (roomId == _state.ActiveRoomId)
            return RoomWatchOutcome.InThatRoom;

        var existing = _state.Watches.GetValueOrDefault(roomId);

        // A lease that ran out before the presence's tick ended it: the watch starts again, its
        // first view and all, as after the tick. The room may have let go of it meanwhile (its
        // hold had the same lease); a hold of this plugin's the room still has, it renews below.
        if (existing is not null && existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await EndWatchAsync(roomId, releaseHold: existing.Plugin != request.Plugin);
            existing = null;
        }

        if (existing is not null && existing.Plugin != request.Plugin)
            return RoomWatchOutcome.WatchedForAnotherPlugin;

        if (existing is null && _state.Watches.Count >= RoomWatchLimits.MAX_WATCHES_PER_PLAYER)
            return RoomWatchOutcome.TooManyWatches;

        var room = _grainFactory.GetRoomGrain(roomId);

        // As if the player walked in, asked again on each renewal so a ban since ends it. A full
        // room still shows: a watcher takes no place in it.
        var access = await room.CheckEntryAccessAsync(
            _state.PlayerId,
            null,
            request.ThroughLinkedPair,
            ct
        );

        RoomWatchOutcome? refused = access switch
        {
            RoomEntryAccessType.Allowed or RoomEntryAccessType.Full => null,
            RoomEntryAccessType.Banned => RoomWatchOutcome.Banned,
            _ => RoomWatchOutcome.DoorClosed,
        };

        if (refused is { } outcome)
        {
            if (existing is not null)
                await EndWatchAsync(roomId, releaseHold: true);

            return outcome;
        }

        await room.EnsureRoomActiveAsync(ct);

        var lease = RoomWatchLimits.Clamp(TimeSpan.FromMilliseconds(request.LeaseMs));
        var held = await room.HoldAsync(
            new RoomHold
            {
                Plugin = request.Plugin,
                Watcher = _state.PlayerId,
                LeaseMs = (long)lease.TotalMilliseconds,
            },
            ct
        );

        if (held != RoomHoldOutcome.Held)
            return RoomWatchOutcome.NotHeld;

        if (existing is not null)
        {
            existing.Envelope = request.Envelope;
            existing.ExpiresAtUtc = DateTime.UtcNow + lease;

            return RoomWatchOutcome.Watching;
        }

        var watch = new WatchedRoom
        {
            Plugin = request.Plugin,
            Envelope = request.Envelope,
            ExpiresAtUtc = DateTime.UtcNow + lease,
        };

        // In place before the subscription, so the room's first publish finds it.
        _state.Watches[roomId] = watch;

        RoomWatchView view;

        try
        {
            var stream = GetRoomStream(roomId);

            await PurgeStaleSubscriptionsAsync(stream, roomId);
            watch.Subscription = await stream.SubscribeAsync(this);

            // Asked once the subscription is in place, so every publish after the ones the view
            // shows reaches the watch; those that arrive meanwhile wait for it.
            view = await room.GetWatchViewAsync(_state.PlayerId, ct);
        }
        catch
        {
            await EndWatchAsync(roomId, releaseHold: true);

            throw;
        }

        // Ended while the view was on its way: nothing more of the room is for this session.
        if (!ReferenceEquals(_state.Watches.GetValueOrDefault(roomId), watch))
            return RoomWatchOutcome.NotOnline;

        ShowFirstView(watch, view);

        return RoomWatchOutcome.Watching;
    }

    /// <summary>
    /// The room as an entry draws it, before anything it publishes: then the publishes that
    /// arrived meanwhile, less those the view already shows, and from then on each as it comes.
    /// </summary>
    private void ShowFirstView(WatchedRoom watch, RoomWatchView view)
    {
        foreach (var composer in view.Composers)
            EnqueueInEnvelope(composer, watch);

        var arrived = watch.AwaitingView ?? [];

        watch.AwaitingView = null;
        watch.ShownUpTo = view.Sequence;

        foreach (var item in arrived)
            EnqueueWatched(item, watch);

        StartOutgoingFlush();
    }

    public async Task<bool> UnwatchRoomAsync(RoomId roomId, string plugin, CancellationToken ct) =>
        _state.Watches.TryGetValue(roomId, out var watch)
        && watch.Plugin == plugin
        && await EndWatchAsync(roomId, releaseHold: true);

    /// <summary>
    /// A watched room's publish: each composer the session may have, in the watch's envelope.
    /// The room's holds on its players' sends are for the players in it, which the watcher is not.
    /// </summary>
    private void EnqueueWatched(RoomOutboundSnapshot item, WatchedRoom watch)
    {
        if (watch.AwaitingView is { } waiting)
        {
            waiting.Add(item);

            return;
        }

        // Publishes are numbered in the room's turns and arrive in that order, so the first one
        // past the view's is the end of those it showed.
        if (watch.ShownUpTo is { } shown)
        {
            if (item.Sequence <= shown)
                return;

            watch.ShownUpTo = null;
        }

        if (
            _sessionObserver is null
            || item.Composers.IsDefaultOrEmpty
            || (item.ExcludedPlayerIds is not null && item.ExcludedPlayerIds.Contains(PlayerId))
        )
            return;

        foreach (var composer in item.Composers)
            EnqueueInEnvelope(composer, watch);
    }

    private void EnqueueInEnvelope(IComposer composer, WatchedRoom watch)
    {
        if (composer is ICapabilityComposer extension && !AcceptsExtension(extension))
            return;

        Enqueue(new PluginEnvelopeComposer { Outer = watch.Envelope, Inner = composer });
    }

    /// <summary>
    /// Ends the watch: the subscription first, so nothing more of the room arrives, then the
    /// watch's hold on the room (told: the room may be the one ending it).
    /// </summary>
    private async Task<bool> EndWatchAsync(RoomId roomId, bool releaseHold)
    {
        if (!_state.Watches.Remove(roomId, out var watch))
            return false;

        if (watch.Subscription is { } subscription)
        {
            try
            {
                await subscription.UnsubscribeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to unsubscribe player {PlayerId} from watched room {RoomId}",
                    _state.PlayerId,
                    roomId
                );
            }
        }

        if (releaseHold)
            _grainFactory
                .GetRoomGrain(roomId)
                .ReleaseHoldAsync(
                    watch.Plugin,
                    string.Empty,
                    _state.PlayerId,
                    CancellationToken.None
                )
                .LogAndForget(
                    _logger,
                    "release the watch of player {PlayerId} on room {RoomId}",
                    _state.PlayerId,
                    roomId
                );

        return true;
    }

    private async Task EndAllWatchesAsync()
    {
        foreach (var roomId in _state.Watches.Keys.ToList())
            await EndWatchAsync(roomId, releaseHold: true);
    }

    /// <summary>Ends the watches whose lease ran out; the presence's tick asks.</summary>
    internal async Task EndExpiredWatchesAsync(DateTime nowUtc)
    {
        foreach (var (roomId, watch) in _state.Watches.ToList())
            if (watch.ExpiresAtUtc <= nowUtc)
                await EndWatchAsync(roomId, releaseHold: true);
    }
}
