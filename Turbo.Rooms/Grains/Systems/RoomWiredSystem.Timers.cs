using System;
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

    /// <summary>
    /// Restarts the timer that "time elapsed" and "at given time" wired read, and the periodic
    /// triggers too.
    /// </summary>
    public void ResetTimers(long now)
    {
        _timersEpochMs = now;
        _timersEpochVersion++;

        // Habbo's Timer Reset "resets triggers that use timers" (furnidata): a repeater it resets
        // fires on the next avatar tick. In its own 3 s repeater's stack it fired the stack on
        // every 500 ms tick, 19 times in 9.5 s, and 3 s apart again once the box was moved off
        // (official client capture 2026-10-24, WIN63-202610201032, wired matrix action.1.R2).
        // That a reset restarts repeaters on other stacks too is inferred from the furnidata.
        foreach (var (_, trigger) in _timedTriggers)
        {
            if (trigger is WiredTriggerPeriodically periodic)
                _nextPeriodicAtMs[periodic.ObjectId] = ResetPeriodicAt(
                    now,
                    periodic.GetPeriodicDelayMs()
                );
        }
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
    /// room's first pass with someone in it, where the entering user is first shown,
    /// as Habbo's do: a room unloaded and loaded again had its 2 s, 5 s and 500 ms repeaters flash
    /// together in the entering user's first UserUpdate, about 470 ms after RoomReady (official
    /// client capture 2026-10-24, WIN63-202610201032, 2/2). One placed since the load waits a
    /// period (<see cref="FirstPeriodicAt"/>).
    /// </summary>
    private long LoadedPeriodicAt(long now)
    {
        var avatarTickMs = RoomConfig.AvatarTickMs;

        return avatarTickMs > 0 ? _roomGrain.AlignToNextBoundary(now, avatarTickMs) : now;
    }

    /// <summary>
    /// When a periodic trigger fires after a Timer Reset: on the next avatar tick, or a period
    /// from now for one shorter than an avatar tick.
    /// </summary>
    private long ResetPeriodicAt(long now, long delayMs)
    {
        var avatarTickMs = RoomConfig.AvatarTickMs;

        return avatarTickMs > 0
            ? Math.Min(now + delayMs, _roomGrain.AlignToNextBoundary(now + 1, avatarTickMs))
            : now + delayMs;
    }

    private async Task ProcessTimedTriggersAsync(long now, CancellationToken ct)
    {
        var elapsedMs = GetElapsedTimerMs(now);
        var restartPeriodic = _restartPeriodicTriggers;
        var roomLoad = !_timedTriggersStarted;

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

                    // A box new to the room whose time the timer has passed already missed it and
                    // waits for the next room load or Timer Reset: a box saved at 1 s in a room
                    // loaded 72 minutes earlier never fired, and fired about 1.45 s after the next
                    // load (official client capture 2026-10-24, wired matrix trigger.3.R5, 4/4).
                    if (
                        !_atTimeFiredVersion.ContainsKey(objectId)
                        && !State.LoadedItemSnapshots.ContainsKey(objectId)
                        && elapsedMs >= atTime.GetTargetMs()
                    )
                    {
                        _atTimeFiredVersion[objectId] = _timersEpochVersion;

                        continue;
                    }

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
