using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Delayed work that runs inside the room tick: a dice that lands after rolling, a wheel that
/// stops, a one-way door that closes behind the avatar. Callbacks run on the grain's own turn,
/// so they may touch room state freely. Timers are keyed by the object that owns them, so an
/// item can replace or cancel its pending work and everything dies with the item on pickup.
/// </summary>
public sealed class RoomTimerSystem(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    private readonly Dictionary<RoomObjectId, RoomTimer> _timersByObjectId = [];
    private readonly PriorityQueue<(RoomObjectId objectId, long version), long> _schedule = new();

    private long _nextVersion;

    /// <summary>
    /// Runs <paramref name="callback"/> once after <paramref name="delayMs"/>. A timer already
    /// pending for the same object is replaced.
    /// </summary>
    public void Schedule(RoomObjectId objectId, int delayMs, Func<CancellationToken, Task> callback)
    {
        var timer = new RoomTimer
        {
            Version = ++_nextVersion,
            DueAtMs = _roomGrain.NowMs() + Math.Max(0, delayMs),
            Callback = callback,
        };

        _timersByObjectId[objectId] = timer;
        _schedule.Enqueue((objectId, timer.Version), timer.DueAtMs);
    }

    public bool IsPending(RoomObjectId objectId) => _timersByObjectId.ContainsKey(objectId);

    public void Cancel(RoomObjectId objectId) => _timersByObjectId.Remove(objectId);

    public async Task ProcessTimersAsync(long now, CancellationToken ct)
    {
        // A callback that schedules its own next run (a clock's tick) waits for the next pass,
        // even when that run is already due; otherwise one pass would run it over and over.
        var lastVersionBeforePass = _nextVersion;

        while (_schedule.TryPeek(out var entry, out var dueAtMs) && dueAtMs <= now)
        {
            var (objectId, version) = entry;

            if (version > lastVersionBeforePass)
                break;

            _schedule.Dequeue();

            // A replaced or cancelled timer leaves a stale queue entry behind; skip it.
            if (!_timersByObjectId.TryGetValue(objectId, out var timer) || timer.Version != version)
                continue;

            _timersByObjectId.Remove(objectId);

            try
            {
                await timer.Callback(ct);
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogError(
                    ex,
                    "Timer for object {ObjectId} failed in room {RoomId}",
                    objectId,
                    _roomGrain.RoomId
                );
            }
        }
    }

    private sealed class RoomTimer
    {
        public required long Version { get; init; }
        public required long DueAtMs { get; init; }
        public required Func<CancellationToken, Task> Callback { get; init; }
    }
}
