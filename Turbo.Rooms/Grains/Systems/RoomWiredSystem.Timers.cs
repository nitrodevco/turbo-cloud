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
    private bool _restartPeriodicTriggers;
    private bool _timedTriggersStarted;

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

    /// <summary>
    /// A Wired Disabler was switched off: every periodic trigger starts its period over, counted
    /// in avatar ticks from the one before the switch, so a 1 s timer fires on the second avatar
    /// tick after it (official client capture, 2026-10-18: +866 and +848 ms, where the schedule
    /// it kept while the disabler was on said +361). Picking up a switched-on disabler keeps the
    /// schedule (+240 ms, on the old phase, 1/1).
    /// </summary>
    public void RestartPeriodicTriggers() => _restartPeriodicTriggers = true;

    private void ForgetTimedTrigger(RoomObjectId objectId)
    {
        _nextPeriodicAtMs.Remove(objectId);
        _atTimeFiredVersion.Remove(objectId);
    }

    /// <summary>
    /// When a periodic trigger first fires, a period from now. A period of whole avatar ticks
    /// starts on an avatar tick, so every firing lands in the room tick where walking avatars
    /// step, after they did: Habbo pushes a walking user in the same millisecond as its step
    /// (official client capture, a 0.5 s repeater with Move/Rotate User: UserUpdate and
    /// WiredMovements at the same ms), and a push between steps snapped the walk back.
    /// </summary>
    private long FirstPeriodicAt(long now, long delayMs)
    {
        var avatarTickMs = RoomConfig.AvatarTickMs;

        return avatarTickMs > 0 && delayMs % avatarTickMs == 0
            ? _roomGrain.AlignToNextBoundary(now + delayMs, avatarTickMs)
            : now + delayMs;
    }

    /// <summary>
    /// When a restarted periodic trigger fires next: a period of whole avatar ticks counts from
    /// the avatar tick before the restart, the next one (or this tick, on one) being the first.
    /// </summary>
    private long RestartedPeriodicAt(long now, long delayMs)
    {
        var avatarTickMs = RoomConfig.AvatarTickMs;

        return avatarTickMs > 0 && delayMs % avatarTickMs == 0
            ? _roomGrain.AlignToNextBoundary(now, avatarTickMs) + delayMs - avatarTickMs
            : now + delayMs;
    }

    /// <summary>
    /// When a periodic trigger the room loaded with first fires: on the first avatar tick of the
    /// room's first pass with someone in it, where the entering user is first shown
    /// (<see cref="WiredConfig.PeriodicTriggersFireOnRoomLoad"/>). One placed since the load waits
    /// a period (<see cref="FirstPeriodicAt"/>).
    /// </summary>
    private long LoadedPeriodicAt(long now)
    {
        var avatarTickMs = RoomConfig.AvatarTickMs;

        return avatarTickMs > 0 ? _roomGrain.AlignToNextBoundary(now, avatarTickMs) : now;
    }

    private async Task ProcessTimedTriggersAsync(long now, CancellationToken ct)
    {
        var elapsedMs = GetElapsedTimerMs(now);
        var restartPeriodic = _restartPeriodicTriggers;
        var roomLoad = !_timedTriggersStarted && WiredConfig.PeriodicTriggersFireOnRoomLoad;

        _restartPeriodicTriggers = false;
        _timedTriggersStarted = true;

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

                    if (restartPeriodic)
                        _nextPeriodicAtMs[objectId] = RestartedPeriodicAt(now, delayMs);

                    if (!_nextPeriodicAtMs.TryGetValue(objectId, out var nextAt))
                    {
                        nextAt =
                            roomLoad && State.LoadedItemSnapshots.ContainsKey(objectId)
                                ? LoadedPeriodicAt(now)
                                : FirstPeriodicAt(now, delayMs);

                        _nextPeriodicAtMs[objectId] = nextAt;
                    }

                    if (now < nextAt)
                        continue;

                    // The next firing counts from when this one was due, not from the tick that
                    // ran it: ticks come a few ms late, and counting from them slowed a 20 Hz
                    // timer to about 13 firings a second (Habbo keeps 20: official client capture,
                    // 222 firings in about 11 s). A room more than a period behind starts over.
                    var next = nextAt + delayMs;

                    _nextPeriodicAtMs[objectId] = next > now ? next : FirstPeriodicAt(now, delayMs);

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
