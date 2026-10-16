using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Players.Grains;

/// <summary>
/// Holds: a player's direct composers waiting behind a room publish they must follow. A room
/// publishes through the room stream, which reaches this grain later than a direct send made
/// after it, so a grain that tells the room to send something (without awaiting it: the room may
/// be waiting on that grain) and then sends the player its own composers would see its own go
/// first. Holding them here, the one place every route meets, keeps the order without anyone
/// awaiting the room. Room publishes are never held: they are what a hold waits for.
/// </summary>
internal sealed partial class PlayerPresenceGrain
{
    private sealed record HoldEntry(DateTime GivesUpAtUtc, IGrainTimer? Timer);

    // Holds in force, by token.
    private readonly Dictionary<long, HoldEntry> _holds = [];

    // Direct composers sent while a hold is in force, in the order they were sent.
    private readonly Queue<IComposer> _held = new();

    private long _lastHoldToken;

    public Task<OutboundHold> HoldForRoomAsync(CancellationToken ct)
    {
        if (
            _playerConfig.OutboundHoldMs <= 0
            || _sessionObserver is null
            || _state.ActiveRoomId <= 0
        )
            return Task.FromResult(OutboundHold.None);

        var token = ++_lastHoldToken;
        var wait = TimeSpan.FromMilliseconds(_playerConfig.OutboundHoldMs);

        // A publish that never comes (the room faulted, the stream dropped it) costs the player
        // at most this long; a direct send after it lets the hold go too, timer or not.
        var timer = this.RegisterGrainTimer<object?>(
            static (self, _) =>
            {
                ((PlayerPresenceGrain)self!).ReleaseOverdueHolds();

                return Task.CompletedTask;
            },
            this,
            new GrainTimerCreationOptions { DueTime = wait, Period = Timeout.InfiniteTimeSpan }
        );

        _holds[token] = new(DateTime.UtcNow + wait, timer);

        return Task.FromResult(new OutboundHold(PlayerId, token));
    }

    public Task ReleaseHoldAsync(OutboundHold hold, CancellationToken ct)
    {
        ReleaseHold(hold);

        return Task.CompletedTask;
    }

    private void ReleaseHold(OutboundHold hold)
    {
        if (hold.PlayerId != PlayerId || !_holds.Remove(hold.Token, out var entry))
            return;

        entry.Timer?.Dispose();
        SendHeldIfFree();
    }

    private void ReleaseOverdueHolds()
    {
        var now = DateTime.UtcNow;

        foreach (var (token, entry) in _holds.Where(x => x.Value.GivesUpAtUtc <= now).ToList())
        {
            _holds.Remove(token);
            entry.Timer?.Dispose();

            _logger.LogDebug(
                "Hold {Token} for player {PlayerId} gave up waiting for its room publish",
                token,
                _state.PlayerId
            );
        }

        SendHeldIfFree();
    }

    /// <summary>Leaving the room: no publish of it will come, so what waited goes now.</summary>
    private void ReleaseAllHolds()
    {
        foreach (var entry in _holds.Values)
            entry.Timer?.Dispose();

        _holds.Clear();
        SendHeldIfFree();
    }

    /// <summary>A session change: what waited was the old session's, as the queue is.</summary>
    private void DropHolds()
    {
        foreach (var entry in _holds.Values)
            entry.Timer?.Dispose();

        _holds.Clear();
        _held.Clear();
    }

    private void SendHeldIfFree()
    {
        if (_holds.Count > 0 || _held.Count == 0)
            return;

        while (_held.TryDequeue(out var composer))
            Enqueue(composer);

        StartOutgoingFlush();
    }

    /// <summary>A composer another grain sent: queued, or held behind a room publish.</summary>
    private void EnqueueDirect(IComposer composer)
    {
        if (_holds.Count > 0)
            ReleaseOverdueHolds();

        if (_holds.Count == 0)
        {
            Enqueue(composer);

            return;
        }

        if (_held.Count >= _playerConfig.MaxPendingComposers)
        {
            _logger.LogWarning(
                "Held composers for player {PlayerId} are full ({Max}); dropping oldest composer",
                _state.PlayerId,
                _playerConfig.MaxPendingComposers
            );

            _held.Dequeue();
        }

        _held.Enqueue(composer);
    }
}
