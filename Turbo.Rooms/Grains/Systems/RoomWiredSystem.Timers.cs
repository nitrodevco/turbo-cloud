using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The room wired timer: the elapsed time that "time elapsed" conditions and the "at given
/// time" trigger read, reset by the "reset timers" action. Periodic triggers are paced here too.
/// </summary>
public sealed partial class RoomWiredSystem
{
    private readonly Dictionary<RoomObjectId, long> _nextPeriodicAtMs = [];
    private readonly Dictionary<RoomObjectId, long> _atTimeFiredVersion = [];
    private long _timersEpochMs = -1;
    private long _timersEpochVersion = 0;

    /// <summary>Restarts the timer that "time elapsed" and "at given time" wired read.</summary>
    public void ResetTimers(long now)
    {
        _timersEpochMs = now;
        _timersEpochVersion++;
    }

    /// <summary>Milliseconds since the timers were last reset (or since wired first ran).</summary>
    public long GetElapsedTimerMs(long now)
    {
        if (_timersEpochMs < 0)
            _timersEpochMs = now;

        return now - _timersEpochMs;
    }

    public int GetElapsedTimerPulses(long now) => WiredPulses.FromMs(GetElapsedTimerMs(now));

    private void ForgetTimedTrigger(RoomObjectId objectId)
    {
        _nextPeriodicAtMs.Remove(objectId);
        _atTimeFiredVersion.Remove(objectId);
    }

    private async Task ProcessTimedTriggersAsync(long now, CancellationToken ct)
    {
        var elapsedMs = GetElapsedTimerMs(now);

        // Listed when the stacks were rebuilt, in the order the stacks and their triggers are
        // walked there, which is the order this loop used to find them in.
        foreach (var (stack, trigger) in _timedTriggers)
        {
            switch (trigger)
            {
                case WiredTriggerPeriodically periodic:
                {
                    var objectId = periodic.ObjectId;
                    var delayMs = periodic.GetPeriodicDelayMs();

                    if (!_nextPeriodicAtMs.TryGetValue(objectId, out var nextAt))
                    {
                        _nextPeriodicAtMs[objectId] = now + delayMs;

                        continue;
                    }

                    if (now < nextAt)
                        continue;

                    // The next firing counts from when this one was due, not from the tick that
                    // ran it: ticks come a few ms late, and counting from them slowed a 20 Hz
                    // timer to about 13 firings a second (Habbo keeps 20: official client capture,
                    // 222 firings in about 11 s). A room more than a period behind starts over.
                    var next = nextAt + delayMs;

                    _nextPeriodicAtMs[objectId] = next > now ? next : now + delayMs;

                    await FireTimedTriggerAsync(trigger, stack, now, ct);

                    break;
                }
                case WiredTriggerAtTime atTime:
                {
                    var objectId = atTime.ObjectId;

                    if (
                        _atTimeFiredVersion.TryGetValue(objectId, out var firedVersion)
                        && firedVersion == _timersEpochVersion
                    )
                        continue;

                    if (elapsedMs < atTime.GetTargetMs())
                        continue;

                    _atTimeFiredVersion[objectId] = _timersEpochVersion;

                    await FireTimedTriggerAsync(trigger, stack, now, ct);

                    break;
                }
            }
        }
    }

    /// <summary>A periodic or "at given time" trigger firing starts an execution of its own.</summary>
    private Task FireTimedTriggerAsync(
        IWiredTrigger trigger,
        IWiredStack stack,
        long now,
        CancellationToken ct
    ) =>
        RunExecutionAsync(
            () =>
                FireTriggerWithEventAsync(
                    trigger,
                    new PeriodicRoomEvent
                    {
                        RoomId = _roomGrain.RoomId,
                        CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                    },
                    stack,
                    now,
                    ct
                ),
            now,
            ct
        );
}
