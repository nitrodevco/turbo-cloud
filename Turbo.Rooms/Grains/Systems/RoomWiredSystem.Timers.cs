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

        foreach (var stack in _stacksById.Values)
        {
            foreach (var trigger in stack.Triggers)
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

                        _nextPeriodicAtMs[objectId] = now + delayMs;

                        await FireTriggerWithEventAsync(
                            trigger,
                            new PeriodicRoomEvent
                            {
                                RoomId = _roomGrain.RoomId,
                                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                            },
                            stack,
                            now,
                            ct
                        );

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

                        await FireTriggerWithEventAsync(
                            trigger,
                            new PeriodicRoomEvent
                            {
                                RoomId = _roomGrain.RoomId,
                                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                            },
                            stack,
                            now,
                            ct
                        );

                        break;
                    }
                }
            }
        }
    }
}
