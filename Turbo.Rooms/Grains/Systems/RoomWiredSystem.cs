using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Storage;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Runs the wired of a room. Boxes on one tile form a stack; an event that a stack trigger
/// accepts runs the selectors, addons and conditions of that stack and schedules its actions.
/// Signals, stack calls, periodic triggers and timers are driven from here as well.
/// </summary>
public sealed partial class RoomWiredSystem(RoomGrain roomGrain)
    : RoomGrainComponent(roomGrain),
        IRoomEventListener
{
    private readonly HashSet<int> _dirtyStackIds = [];
    private readonly Dictionary<int, IWiredStack> _stacksById = [];
    private readonly Dictionary<Type, List<int>> _stackIdsByEventType = [];
    private readonly Dictionary<int, int> _nextUnseenIndexByStackId = [];
    private readonly Queue<RoomEvent> _eventQueue = new();

    // Floor furni whose state wired changed since the last state update. Habbo sends them as one
    // ObjectsDataUpdate per wired tick: the tiles the effects changed and the boxes that flashed
    // (WiredConfig.StateUpdateMs).
    private readonly HashSet<RoomObjectId> _pendingFloorStates = [];
    private bool _collectingTickStates;
    private long _nextStateUpdateMs;
    private readonly Dictionary<
        WiredExecutionKey,
        WiredPendingStackExecution
    > _pendingStackExecutions = [];
    private readonly PriorityQueue<(WiredExecutionKey key, long version), long> _stackSchedule =
        new();

    // The periodic and "at given time" triggers of every stack, listed when the stacks are
    // rebuilt, so the tick does not walk every trigger of every stack to find them.
    private readonly List<(IWiredStack Stack, IWiredTrigger Trigger)> _timedTriggers = [];

    // Scratch lists for a filter selector, reused across firings.
    private readonly List<int> _keptFurni = [];
    private readonly List<RoomObjectId> _keptAvatars = [];

    // Events turned away because the queue was full, reported once per tick.
    private int _droppedEvents;

    private int _tickMs => _roomGrain._wiredConfig.TickMs;
    private bool _firstRun = true;
    private long _nextStackExecutionId = 0;

    // The execution running now; its signals and stack calls run inside it.
    private WiredExecution? _execution;

    // Signal and call runs per stack in the current visit window.
    private readonly Dictionary<int, int> _visitsByStackId = [];
    private long _visitWindowStartMs = long.MinValue;

    // The wall clock the execution time limits read.
    private Func<long> _wallClockMs = roomGrain.NowMs;

    /// <param name="dormant">
    /// Nobody is in the room: periodic and "at given time" triggers wait until someone is, and
    /// fire once then rather than for every period missed. Events and scheduled actions still run.
    /// </param>
    public async Task ProcessWiredAsync(long now, bool dormant, CancellationToken ct)
    {
        if (now < _roomGrain._state.NextWiredBoundaryMs)
            return;

        while (now >= _roomGrain._state.NextWiredBoundaryMs)
            _roomGrain._state.NextWiredBoundaryMs += _tickMs;

        if (_droppedEvents > 0)
        {
            _roomGrain._logger.LogWarning(
                "Wired event queue of room {RoomId} was full ({Max}); {Count} events were dropped",
                _roomGrain.RoomId,
                _roomGrain._wiredConfig.MaxQueuedEvents,
                _droppedEvents
            );

            _droppedEvents = 0;
        }

        _collectingTickStates = true;

        try
        {
            await ProcessWiredTickAsync(now, dormant, ct);
        }
        finally
        {
            _collectingTickStates = false;

            RollHeavyWindow(now);

            if (now >= _nextStateUpdateMs)
            {
                var stateUpdateMs = Math.Max(1, _roomGrain._wiredConfig.StateUpdateMs);
                _nextStateUpdateMs = now - now % stateUpdateMs + stateUpdateMs;

                FlushPendingFloorStates();
            }
        }
    }

    private async Task ProcessWiredTickAsync(long now, bool dormant, CancellationToken ct)
    {
        if (_firstRun)
        {
            await ProcessInternalVariablesAsync(now, ct);

            _firstRun = false;
        }

        await ProcessVariableBoxesAsync(now, ct);
        await ProcessWiredStacksAsync(now, ct);

        // A KILLED room runs no wired until the halt ends; what happens meanwhile is not kept.
        if (IsKilled(now))
        {
            _eventQueue.Clear();

            return;
        }

        await RunDueScheduledStackExecutionsAsync(now, ct);

        if (_stacksById.Count == 0)
        {
            _eventQueue.Clear();

            return;
        }

        if (!dormant)
            await ProcessTimedTriggersAsync(now, ct);

        var budget = _roomGrain._wiredConfig.MaxEventsPerTick;

        while (budget-- > 0 && _eventQueue.Count > 0)
        {
            var evt = _eventQueue.Dequeue();

            if (evt is WiredSignalEvent signal)
                FlashAntennas(signal);

            await RunExecutionAsync(() => ProcessRoomEventAsync(evt, now, ct), now, ct);
        }
    }

    /// <summary>
    /// Runs one execution: <paramref name="start"/> fires the stacks it starts with, then every
    /// action chain they and the signals and stack calls they make choose runs, in the order
    /// chosen, until none is left or the execution is cancelled.
    /// </summary>
    private async Task RunExecutionAsync(Func<Task> start, long now, CancellationToken ct)
    {
        if (_execution is not null)
        {
            await start();

            return;
        }

        var execution = new WiredExecution(now, _wallClockMs());
        var startedTicks = Stopwatch.GetTimestamp();

        _execution = execution;

        try
        {
            await start();

            while (!execution.IsStopped && execution.Ready.TryDequeue(out var next))
            {
                if (await ExecuteStackChainAsync(next.Key, next.Pending, now, ct))
                    _pendingStackExecutions.Remove(next.Key);
            }
        }
        finally
        {
            _execution = null;

            // Only executions count as the room's wired time: loading boxes and building stacks,
            // which a room does once when it wakes, is not.
            _busyTicksInWindow += Stopwatch.GetTimestamp() - startedTicks;

            KillIfOverrun(execution);
        }
    }

    /// <summary>
    /// A signal or stack call made inside an execution reaches its stacks at once: their
    /// selectors and conditions read the room as it is when it is sent, and the actions they
    /// choose join the execution (official client capture: receivers see the state from when the
    /// signal was sent, and two receivers on one antenna do not see each other's changes).
    /// </summary>
    private Task DeliverInExecutionAsync(
        RoomEvent evt,
        WiredExecution execution,
        CancellationToken ct
    )
    {
        if (execution.IsStopped)
            return Task.CompletedTask;

        var timeoutMs = _roomGrain._wiredConfig.RecursionTimeoutMs;

        if (timeoutMs > 0 && _wallClockMs() - execution.StartedAtWallMs > timeoutMs)
        {
            if (execution.Stop())
                RecordError(WiredErrorKind.RecursionTimeout, execution.Now);

            return Task.CompletedTask;
        }

        if (evt is WiredSignalEvent signal)
            FlashAntennas(signal);

        return ProcessRoomEventAsync(evt, execution.Now, ct);
    }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is null)
            return Task.CompletedTask;

        if (_execution is { } execution && evt is WiredSignalEvent or WiredStackCalledEvent)
            return DeliverInExecutionAsync(evt, execution, ct);

        switch (evt)
        {
            case RoomWiredStackChangedEvent stackEvt:
                {
                    foreach (var stackId in stackEvt.StackIds)
                        _dirtyStackIds.Add(stackId);
                }
                break;
            case WiredVariableBoxChangedEvent boxEvt:
                {
                    foreach (var boxId in boxEvt.BoxIds)
                        _dirtyVariableBoxIds.Add(boxId);
                }
                break;
            case PlayerControllerLevelChangedEvent levelEvt:
                return SendPermissionsAsync(levelEvt.PlayerId, levelEvt.ControllerLevel, ct);
            case PlayerLeftEvent playerLeftEvt:
                _playerActiveStore.RemoveAvatarStore(playerLeftEvt.ObjectId);
                QueueEvent(evt);
                break;
            case RoomItemDetachedEvent detatchedEvt:
                _furnitureActiveStore.RemoveFurnitureStore(detatchedEvt.ObjectId);
                ForgetStoredValuesOfUnownedFurni(detatchedEvt.ObjectId);
                ForgetProjectileFlight(detatchedEvt.ObjectId);
                ForgetTimedTrigger(detatchedEvt.ObjectId);
                break;
            default:
                QueueEvent(evt);
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Keeps an event for the next wired tick when a stack could want it: a stack call, an event
    /// type some trigger listens to, or anything while stacks are waiting to be rebuilt (a box
    /// placed a moment ago may listen to it). Every walk, chat and item change used to be
    /// queued in any room with a wired box in it, only to be looked up and dropped. The queue
    /// is bounded by <c>WiredConfig.MaxQueuedEvents</c>.
    /// </summary>
    private void QueueEvent(RoomEvent evt)
    {
        if (
            evt is not WiredStackCalledEvent
            && _dirtyStackIds.Count == 0
            && !_stackIdsByEventType.ContainsKey(evt.GetType())
        )
            return;

        if (_eventQueue.Count >= _roomGrain._wiredConfig.MaxQueuedEvents)
        {
            _droppedEvents++;
            RecordError(WiredErrorKind.ExecutorOverload, _roomGrain.NowMs());

            return;
        }

        _eventQueue.Enqueue(evt);
    }

    private async Task ProcessRoomEventAsync(RoomEvent evt, long now, CancellationToken ct)
    {
        if (evt is null)
            return;

        if (evt is WiredStackCalledEvent callEvt)
        {
            await ProcessStackCallAsync(callEvt, now, ct);

            return;
        }

        if (!_stackIdsByEventType.TryGetValue(evt.GetType(), out var stackIds))
            return;

        foreach (var stackId in stackIds)
        {
            if (!_stacksById.TryGetValue(stackId, out var stack) || stack is null)
                continue;

            foreach (var trigger in stack.Triggers)
                await FireTriggerWithEventAsync(trigger, evt, stack, now, ct);
        }
    }

    private async Task FireTriggerWithEventAsync(
        IWiredTrigger trigger,
        RoomEvent evt,
        IWiredStack stack,
        long now,
        CancellationToken ct
    )
    {
        if (
            trigger is null
            || evt is null
            || stack is null
            || !await trigger.MatchesEventAsync(evt, ct)
        )
            return;

        var (signal, depth) = evt switch
        {
            WiredSignalEvent signalEvt => (
                new WiredSelectionSet().UnionWith(
                    new WiredSelectionSet(signalEvt.FurniIds, signalEvt.AvatarIds)
                ),
                signalEvt.Depth
            ),
            _ => (new WiredSelectionSet(), 0),
        };

        var ctx = new WiredProcessingContext(_roomGrain)
        {
            Event = evt,
            Stack = stack,
            Trigger = trigger,
            Signal = signal,
            Depth = depth,
            CancellationToken = ct,
            // A signalled stack starts from a copy of what the sender's context held.
            ContextValues = ContextValuesFor(evt),
            CarriedPlaceholders = PlaceholdersFor(evt),
        };

        // "User Says Keyword" gives its stack $(trigger_text): what was said. It is carried, so
        // it reaches delayed effects and the stacks a signal from here calls.
        if (trigger is WiredTriggerHabboSaysKeyword && evt is PlayerChatEvent chat)
            ctx.CarriedPlaceholders[WiredPlaceholderText.TriggerTextToken] = chat.Text;

        SeedSelectionFromEvent(ctx, evt);

        var selection = ctx.GetSelection(trigger);

        // A furni event names its furni, and that one is "the triggering item": the box's other
        // picks only say which furni it listens to (Wired Faculty, "Making counters with number
        // blocks": the block that just became 0 is the triggering item).
        if (trigger is IWiredFurniEventTrigger)
            ctx.Selected.SelectedAvatarIds.UnionWith(selection.SelectedAvatarIds);
        else
            ctx.Selected.UnionWith(selection);

        await RunStackAsync(ctx, now, null, ct);
    }

    /// <summary>
    /// Runs the selectors, addons and conditions of a stack for one firing and schedules the
    /// actions the outcome selects. <paramref name="negativeCall"/> is set for stack calls: a
    /// positive call runs the actions when the conditions pass, a negative one when they fail.
    /// </summary>
    private async Task RunStackAsync(
        WiredProcessingContext ctx,
        long now,
        bool? negativeCall,
        CancellationToken ct
    )
    {
        if (
            ctx.Event is WiredSignalEvent or WiredStackCalledEvent
            && !TryVisitReceiver(ctx.Stack.StackId, now)
        )
            return;

        if (
            _execution is { } execution
            && !ChargeExecution(execution, _roomGrain._wiredConfig.ExecutionCostPerStack)
        )
            return;

        var outer = _contextValues;
        var outerRunning = _running;

        _contextValues = ctx.ContextValues;
        _running = new WiredRunningExecution(ctx.SelectorPool, ctx.Signal, ctx.Event, ctx.Trigger);

        try
        {
            await RunStackCoreAsync(ctx, now, negativeCall, ct);
        }
        finally
        {
            _contextValues = outer;
            _running = outerRunning;
        }
    }

    private async Task RunStackCoreAsync(
        WiredProcessingContext ctx,
        long now,
        bool? negativeCall,
        CancellationToken ct
    )
    {
        foreach (var selector in ctx.Stack.Selectors)
        {
            await ApplySelectorAsync(ctx, selector, ct);
            Flash(selector);
        }

        foreach (var addon in ctx.Stack.Addons)
        {
            if (!await addon.MutatePolicyAsync(ctx, ct))
                return;
        }

        var passed = EvaluateConditions(ctx.Stack.Conditions, ctx);

        if (ctx.Trigger is not null && !await ctx.Trigger.CanTriggerAsync(ctx, ct))
            return;

        // Habbo flashes a trigger each time it fires, whatever its conditions then decide, and the
        // conditions only when they pass (recorded on hhus, WIN63-202610121709: "receive signal"
        // triggers flash every tick while their stack's single condition and effect flash only on
        // the ticks the condition holds).
        if (ctx.Trigger is not null)
            Flash(ctx.Trigger);

        if (passed)
        {
            foreach (var condition in ctx.Stack.Conditions)
                Flash(condition);
        }

        if (negativeCall is bool negative && negative == passed)
            return;

        // A stack call runs the positive actions whichever way it went; a trigger runs the side
        // its conditions chose.
        var runNegative = negativeCall is null && !passed;
        var pool = new List<IWiredAction>(ctx.Stack.Actions.Count);

        foreach (var action in ctx.Stack.Actions)
        {
            if (action.IsNegative == runNegative)
                pool.Add(action);
        }

        if (pool.Count == 0)
            return;

        foreach (var addon in ctx.Stack.Addons)
            await addon.BeforeEffectsAsync(ctx, ct);

        ScheduleStackExecution(ctx, ChooseActions(pool, ctx.Policy, ctx.Stack.StackId), now);

        foreach (var addon in ctx.Stack.Addons)
            await addon.AfterEffectsAsync(ctx, ct);
    }

    /// <summary>
    /// Adds a selector's result to the pool. Filter selectors narrow what is already there
    /// (the pool, or the triggering set when the pool is still empty); inverted selectors
    /// contribute everything in the room they did not pick.
    /// </summary>
    private async Task ApplySelectorAsync(
        WiredProcessingContext ctx,
        IWiredSelector selector,
        CancellationToken ct
    )
    {
        var set = await selector.SelectAsync(ctx, ct);

        if (selector.GetIsInvert())
            set = InvertSelection(set);

        if (!selector.GetIsFilter())
        {
            ctx.SelectorPool.UnionWith(set);

            return;
        }

        var basis =
            ctx.SelectorPool.HasFurni || ctx.SelectorPool.HasAvatars
                ? ctx.SelectorPool
                : ctx.Selected;

        // The pool is rebuilt from the kept ids in the basis' own order, as before, but through
        // buffers this system keeps: the basis can be the pool itself, so it is read out first.
        _keptFurni.Clear();
        _keptAvatars.Clear();

        foreach (var furniId in basis.SelectedFurniIds)
        {
            if (set.SelectedFurniIds.Contains(furniId))
                _keptFurni.Add(furniId);
        }

        foreach (var avatarId in basis.SelectedAvatarIds)
        {
            if (set.SelectedAvatarIds.Contains(avatarId))
                _keptAvatars.Add(avatarId);
        }

        ctx.SelectorPool.SelectedFurniIds.Clear();
        ctx.SelectorPool.SelectedAvatarIds.Clear();
        ctx.SelectorPool.SelectedFurniIds.UnionWith(_keptFurni);
        ctx.SelectorPool.SelectedAvatarIds.UnionWith(_keptAvatars);
    }

    private WiredSelectionSet InvertSelection(IWiredSelectionSet set)
    {
        var inverted = new WiredSelectionSet();

        foreach (var item in FurniModule.Items)
        {
            if (!set.SelectedFurniIds.Contains(item.ObjectId))
                inverted.SelectedFurniIds.Add(item.ObjectId);
        }

        foreach (var avatar in AvatarModule.Avatars)
        {
            if (!set.SelectedAvatarIds.Contains(avatar.ObjectId))
                inverted.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return inverted;
    }

    /// <summary>The furni and users an event is about become the triggering selection.</summary>
    private void SeedSelectionFromEvent(WiredProcessingContext ctx, RoomEvent evt)
    {
        if (evt.CausedBy.Origin == ActionOrigin.Player && evt.CausedBy.PlayerId > 0)
            AddPlayerById(ctx, evt.CausedBy.PlayerId);

        switch (evt)
        {
            // The one who clicked triggers; the one clicked is "The clicked user" (Wired Faculty,
            // "See who is clicking on who", 19/03/2025: "$(user) clicks on $(target)").
            case PlayerClickedAvatarEvent clickEvt:
                AddPlayerById(ctx, clickEvt.PlayerId);

                if (AvatarModule.TryGetAvatar(clickEvt.TargetObjectId, out _))
                    ctx.EventTargets.SelectedAvatarIds.Add(clickEvt.TargetObjectId);

                break;
            // The click tile clicked is the triggering item, like a clicked furni.
            case PlayerClickedTileEvent tileEvt:
                AddPlayerById(ctx, tileEvt.PlayerId);
                ctx.Selected.SelectedFurniIds.Add(tileEvt.FurniId);
                break;
            case PlayerEvent playerEvt:
                AddPlayerById(ctx, playerEvt.PlayerId);
                break;
            case AvatarWalkOnFurniEvent walkOnEvt:
                AddAvatarByObjectId(ctx, walkOnEvt.ObjectId);
                ctx.Selected.SelectedFurniIds.Add(walkOnEvt.FurniId);
                break;
            case AvatarWalkOffFurniEvent walkOffEvt:
                AddAvatarByObjectId(ctx, walkOffEvt.ObjectId);
                ctx.Selected.SelectedFurniIds.Add(walkOffEvt.FurniId);
                break;
            // Anything else an avatar did names it by room index and nothing more, so the avatar
            // is the whole selection. This follows the two above, which add their furni as well.
            case AvatarEvent avatarEvt:
                AddAvatarByObjectId(ctx, avatarEvt.ObjectId);
                break;
            case RoomItemEvent itemEvt:
                ctx.Selected.SelectedFurniIds.Add(itemEvt.ObjectId);
                break;
            case BotReachedAvatarEvent botAvatarEvt:
                AddAvatarByObjectId(ctx, botAvatarEvt.TargetObjectId);
                break;
            case BotReachedItemEvent botItemEvt:
                ctx.Selected.SelectedFurniIds.Add(botItemEvt.FurniId);
                break;
            case WiredClockTickEvent clockEvt:
                ctx.Selected.SelectedFurniIds.Add(clockEvt.ObjectId);
                break;
            case WiredSignalEvent signalEvt:
                ctx.Selected.SelectedFurniIds.UnionWith(signalEvt.AntennaIds);
                break;
        }
    }

    private void AddAvatarByObjectId(WiredProcessingContext ctx, RoomObjectId objectId)
    {
        if (AvatarModule.TryGetAvatar(objectId, out _))
            ctx.Selected.SelectedAvatarIds.Add(objectId);
    }

    /// <summary>An event names the player; a selection names the avatar they are here.</summary>
    private void AddPlayerById(WiredProcessingContext ctx, PlayerId playerId)
    {
        if (AvatarModule.TryGetPlayer(playerId, out var player))
            ctx.Selected.SelectedAvatarIds.Add(player.ObjectId);
    }

    private async Task ProcessStackCallAsync(
        WiredStackCalledEvent evt,
        long now,
        CancellationToken ct
    )
    {
        foreach (var stackId in evt.StackIds)
        {
            if (!_stacksById.TryGetValue(stackId, out var stack) || stack is null)
                continue;

            var ctx = new WiredProcessingContext(_roomGrain)
            {
                Event = evt,
                Stack = stack,
                Trigger = null,
                Depth = evt.Depth,
                CancellationToken = ct,
                ContextValues = ContextValuesFor(evt),
                CarriedPlaceholders = PlaceholdersFor(evt),
            };

            ctx.Selected.SelectedFurniIds.UnionWith(evt.FurniIds);
            ctx.Selected.SelectedAvatarIds.UnionWith(evt.AvatarIds);

            await RunStackAsync(ctx, now, evt.IsNegative, ct);
        }
    }

    /// <summary>
    /// Counts a signal or stack call running a stack; false once the stack had
    /// <c>WiredConfig.MaxStackVisitsPerWindow</c> of them in the current window. Windows are fixed
    /// <c>StackVisitWindowMs</c> slices of room time, so a chain that used up a stack's visits runs
    /// again in the next one and a delay of half a second always finds fresh visits.
    /// </summary>
    private bool TryVisitReceiver(int stackId, long now)
    {
        var limit = _roomGrain._wiredConfig.MaxStackVisitsPerWindow;

        if (limit <= 0)
            return true;

        var windowMs = Math.Max(1, _roomGrain._wiredConfig.StackVisitWindowMs);
        var windowStart = now - now % windowMs;

        if (windowStart != _visitWindowStartMs)
        {
            _visitsByStackId.Clear();
            _visitWindowStartMs = windowStart;
        }

        _visitsByStackId.TryGetValue(stackId, out var visits);

        if (visits >= limit)
            return false;

        _visitsByStackId[stackId] = visits + 1;

        return true;
    }

    /// <summary>Whether the tile holds a stack with at least one wired box in it.</summary>
    public bool HasStack(int stackId) => _stacksById.ContainsKey(stackId);

    private void ScheduleStackExecution(
        WiredProcessingContext ctx,
        List<IWiredAction> actions,
        long dueAtMs
    )
    {
        if (actions.Count == 0)
            return;

        var key = new WiredExecutionKey(
            ctx.Stack.StackId,
            Interlocked.Increment(ref _nextStackExecutionId)
        );

        var pending = new WiredPendingStackExecution
        {
            Stack = ctx.Stack,
            Actions = actions,
            Trigger = ctx.Trigger,
            Event = ctx.Event,
            Policy = ctx.Policy,
            Selected = ctx.Selected,
            SelectorPool = ctx.SelectorPool,
            Signal = ctx.Signal,
            EventTargets = ctx.EventTargets,
            CarriedPlaceholders = ctx.CarriedPlaceholders,
            Depth = ctx.Depth,
            Version = 1,
            DueAtMs = dueAtMs + (long)ctx.Policy.Delay.TotalMilliseconds,
            NextActionIndex = 0,
            VariableChanges = ctx.Policy.ExecuteInOrder ? null : new WiredVariableChangeBatch(),
            ContextValues = ctx.ContextValues,
        };

        // Without a delay the actions run in the execution that chose them; with one, they are
        // a new execution when the delay is over.
        if (pending.DueAtMs <= dueAtMs && _execution is { IsStopped: false } execution)
        {
            execution.Ready.Enqueue((key, pending));

            return;
        }

        TryHoldPending(key, pending, dueAtMs);
    }

    /// <summary>
    /// Keeps an execution to run when it is due. A room holds at most
    /// <c>WiredConfig.MaxDelayedEvents</c>; one more is dropped and counted as DELAYED_EVENTS_CAP.
    /// </summary>
    private bool TryHoldPending(WiredExecutionKey key, WiredPendingStackExecution pending, long now)
    {
        var cap = _roomGrain._wiredConfig.MaxDelayedEvents;

        if (
            cap > 0
            && !_pendingStackExecutions.ContainsKey(key)
            && _pendingStackExecutions.Count >= cap
        )
        {
            RecordError(WiredErrorKind.DelayedEventsCap, now);

            return false;
        }

        _pendingStackExecutions[key] = pending;
        _stackSchedule.Enqueue((key, pending.Version), pending.DueAtMs);

        return true;
    }

    private async Task RunDueScheduledStackExecutionsAsync(long now, CancellationToken ct)
    {
        var budget = _roomGrain._wiredConfig.MaxScheduledPerTick;

        while (budget-- > 0 && _stackSchedule.Count > 0)
        {
            var (entry, dueAtMs) = PeekSchedule();

            if (dueAtMs > now)
                break;

            _stackSchedule.Dequeue();

            var (key, version) = entry;

            if (
                !_pendingStackExecutions.TryGetValue(key, out var pending)
                || pending.Version != version
            )
                continue;

            if (pending.DueAtMs > now)
                continue;

            // What runs after a delay is a new execution, with fresh budgets.
            await RunExecutionAsync(
                async () =>
                {
                    if (await ExecuteStackChainAsync(key, pending, now, ct))
                        _pendingStackExecutions.Remove(key);
                },
                now,
                ct
            );
        }

        ((WiredExecutionKey key, long version) entry, long dueAtMs) PeekSchedule()
        {
            if (_stackSchedule.TryPeek(out var k, out var p))
                return (k, p);

            return (default, long.MaxValue);
        }
    }

    private async Task<bool> ExecuteStackChainAsync(
        WiredExecutionKey key,
        WiredPendingStackExecution pending,
        long now,
        CancellationToken ct
    )
    {
        for (var i = pending.NextActionIndex; i < pending.Actions.Count; i++)
        {
            var action = pending.Actions[i];

            if (pending.WaitingActionIndex == i)
            {
                if (now < pending.DueAtMs)
                {
                    return false;
                }
                else
                {
                    pending.WaitingActionIndex = null;
                }
            }
            else
            {
                var delayMs = Math.Max(0, action.GetDelayMs());

                if (delayMs > 0)
                {
                    // What ran before the wait is one change; what runs after it is another.
                    await FlushVariableChangesAsync(pending, ct);

                    pending.WaitingActionIndex = i;

                    // Dropped at the delayed-events cap: the rest of the chain does not run.
                    return !RescheduleStack(key, pending, now + delayMs);
                }
            }

            var succeeded = false;

            try
            {
                // The three sets are shared by every action of the chain, not copied for each:
                // an action reads its targets through them and never changes them.
                var ctx = new WiredExecutionContext(_roomGrain)
                {
                    Policy = pending.Policy,
                    Selected = pending.Selected,
                    SelectorPool = pending.SelectorPool,
                    Signal = pending.Signal,
                    EventTargets = pending.EventTargets,
                    CarriedPlaceholders = pending.CarriedPlaceholders,
                    Depth = pending.Depth,
                    CancellationToken = ct,
                    VariableChanges = pending.VariableChanges,
                    ContextValues = pending.ContextValues,
                };

                // An action the room's usage cannot pay for cancels the execution, and with it the
                // rest of this chain.
                if (
                    _execution is { } execution
                    && !ChargeExecution(execution, ActionCost(action, ctx))
                )
                    break;

                action
                    .FlashActivationStateAsync(ct)
                    .LogAndForget(
                        _roomGrain._logger,
                        "flash a wired box in room {RoomId}",
                        _roomGrain.RoomId
                    );

                var outer = _contextValues;
                var outerRunning = _running;

                _contextValues = pending.ContextValues;
                _running = new WiredRunningExecution(
                    ctx.SelectorPool,
                    ctx.Signal,
                    pending.Event,
                    pending.Trigger
                );

                try
                {
                    succeeded = await action.ExecuteAsync(ctx, ct);
                }
                finally
                {
                    _contextValues = outer;
                    _running = outerRunning;
                }

                FlushWiredContext(ctx);
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogWarning(
                    ex,
                    "Wired action {WiredType} {WiredCode} failed in room {RoomId}",
                    action.WiredType,
                    action.WiredCode,
                    _roomGrain.RoomId
                );
            }

            pending.NextActionIndex = i + 1;

            if (succeeded && pending.Policy.ShortCircuitOnFirstEffectSuccess)
                break;
        }

        await FlushVariableChangesAsync(pending, ct);

        return true;
    }

    /// <summary>Makes the held variable changes one change per holder (<see cref="WiredVariableChangeBatch"/>).</summary>
    private async Task FlushVariableChangesAsync(
        WiredPendingStackExecution pending,
        CancellationToken ct
    )
    {
        if (pending.VariableChanges is not { IsEmpty: false } batch)
            return;

        var ctx = new WiredExecutionContext(_roomGrain)
        {
            Policy = pending.Policy,
            Selected = pending.Selected,
            SelectorPool = pending.SelectorPool,
            Signal = pending.Signal,
            EventTargets = pending.EventTargets,
            CarriedPlaceholders = pending.CarriedPlaceholders,
            Depth = pending.Depth,
            CancellationToken = ct,
            ContextValues = pending.ContextValues,
        };

        var outer = _contextValues;
        var outerRunning = _running;

        _contextValues = pending.ContextValues;
        _running = new WiredRunningExecution(
            ctx.SelectorPool,
            ctx.Signal,
            pending.Event,
            pending.Trigger
        );

        try
        {
            await batch.FlushAsync(ctx);
            FlushWiredContext(ctx);
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Combined wired variable change failed in room {RoomId}",
                _roomGrain.RoomId
            );
        }
        finally
        {
            _contextValues = outer;
            _running = outerRunning;
        }
    }

    private bool RescheduleStack(
        WiredExecutionKey key,
        WiredPendingStackExecution pending,
        long dueAtMs
    )
    {
        if (pending.DueAtMs != dueAtMs)
            pending.Version++;

        pending.DueAtMs = dueAtMs;

        return TryHoldPending(key, pending, _execution?.Now ?? dueAtMs);
    }

    /// <summary>
    /// What an action adds to the room's usage: its own cost and one for each furni or user it
    /// works on.
    /// </summary>
    private double ActionCost(IWiredAction action, WiredExecutionContext ctx)
    {
        var config = _roomGrain._wiredConfig;
        var cost = config.ExecutionCostPerAction;

        // A signal's antennas and a call's stacks are where it goes, not what it works on.
        if (
            config.ExecutionCostPerTarget > 0
            && action is not (WiredActionSendSignal or WiredActionCallStacks)
        )
        {
            var targets = ctx.GetSelection(action);

            cost +=
                config.ExecutionCostPerTarget
                * (targets.SelectedFurniIds.Count + targets.SelectedAvatarIds.Count);
        }

        return cost;
    }

    /// <summary>
    /// What one action moved and changed, as one room message in the order it always went out:
    /// the moves, then the floor states, then the wall states. It is one message per action, not
    /// per tick, because an action also reaches the room by other routes (a chat bubble, a furni
    /// put down or taken away) and a later action's moves must not overtake those.
    /// </summary>
    private void FlushWiredContext(WiredExecutionContext ctx)
    {
        var composers = ImmutableArray.CreateBuilder<IComposer>(3);
        var shownOrHidden =
            ctx.HiddenBeforeMove.Count > 0
                ? _roomGrain.FurniModule.TakeHiddenWiredMoves(
                    ctx.FloorItemMoves,
                    ctx.WallItemMoves,
                    ctx.HiddenBeforeMove
                )
                : [];

        if (
            ctx.UserMoves.Count > 0
            || ctx.UserDirections.Count > 0
            || ctx.FloorItemMoves.Count > 0
            || ctx.WallItemMoves.Count > 0
        )
            composers.Add(
                new WiredMovementsMessageComposer
                {
                    Users = ctx.UserMoves,
                    FloorItems = ctx.FloorItemMoves,
                    WallItems = ctx.WallItemMoves,
                    UserDirections = ctx.UserDirections,
                }
            );

        composers.AddRange(shownOrHidden);

        if (ctx.FloorItemStateUpdates.Count > 0)
        {
            if (_collectingTickStates)
            {
                foreach (var (objectId, _) in ctx.FloorItemStateUpdates)
                    _pendingFloorStates.Add(objectId);
            }
            else
                composers.Add(
                    new ObjectsDataUpdateMessageComposer { StuffDatas = ctx.FloorItemStateUpdates }
                );
        }

        if (ctx.WallItemStateUpdates.Count > 0)
            composers.Add(
                new ItemsStateUpdateMessageComposer { ObjectStates = ctx.WallItemStateUpdates }
            );

        _roomGrain.SendComposersToRoomAndForget(composers.ToImmutable());
    }

    private async Task ProcessWiredStacksAsync(long now, CancellationToken ct)
    {
        if (_dirtyStackIds.Count == 0)
            return;

        var dirtyStackIds = _dirtyStackIds.ToList();

        _dirtyStackIds.Clear();

        foreach (var stackId in dirtyStackIds)
            await ProcessWiredStackAsync(stackId, ct);

        _stackIdsByEventType.Clear();
        _timedTriggers.Clear();

        foreach (var stack in _stacksById.Values)
        {
            foreach (var trigger in stack.Triggers)
            {
                if (trigger is WiredTriggerPeriodically or WiredTriggerAtTime)
                    _timedTriggers.Add((stack, trigger));

                foreach (var eventType in trigger.SupportedEventTypes)
                {
                    if (!_stackIdsByEventType.TryGetValue(eventType, out var list))
                    {
                        list = [];

                        _stackIdsByEventType[eventType] = list;
                    }

                    list.Add(stack.StackId);
                }
            }
        }

        await RefreshClickUserEnvironmentAsync(ct);
    }

    private async Task ProcessWiredStackAsync(int stackId, CancellationToken ct)
    {
        _stacksById.Remove(stackId);
        _nextUnseenIndexByStackId.Remove(stackId);

        var wiredItems = FurniModule
            .GetFloorItemsOnTile(stackId)
            .Where(x =>
                x.Logic is FurnitureWiredLogic && x.Logic is not FurnitureWiredVariableLogic
            )
            .OrderBy(x => x.Z.Value)
            .ToList();

        if (wiredItems.Count == 0)
            return;

        var stack = new WiredStack { StackId = stackId };

        foreach (var item in wiredItems)
        {
            try
            {
                var wiredLogic = (FurnitureWiredLogic)item.Logic!;

                await wiredLogic.LoadWiredAsync(ct);

                switch (wiredLogic)
                {
                    case FurnitureWiredTriggerLogic trigger:
                        stack.Triggers.Add(trigger);
                        break;
                    case FurnitureWiredSelectorLogic selector:
                        stack.Selectors.Add(selector);
                        break;
                    case FurnitureWiredConditionLogic condition:
                        stack.Conditions.Add(condition);
                        break;
                    case FurnitureWiredAddonLogic addon:
                        stack.Addons.Add(addon);
                        break;
                    case FurnitureWiredActionLogic effect:
                        stack.Actions.Add(effect);
                        break;
                }
            }
            catch (Exception ex)
            {
                var box = (IWiredBox)item.Logic!;

                _roomGrain._logger.LogWarning(
                    ex,
                    "Failed to load wired item {ObjectId} ({WiredType} {WiredCode}) in room {RoomId}",
                    item.ObjectId,
                    box.WiredType,
                    box.WiredCode,
                    _roomGrain.RoomId
                );
            }
        }

        _stacksById[stackId] = stack;
    }

    private List<IWiredAction> ChooseActions(
        List<IWiredAction> actions,
        IWiredPolicy policy,
        int stackId
    )
    {
        if (actions.Count == 0)
            return [];

        switch (policy.EffectMode)
        {
            case WiredEffectModeType.FirstOnly:
                return [actions[0]];
            case WiredEffectModeType.Random:
            {
                var skip = Math.Min(Math.Max(0, policy.RandomSkipCount), actions.Count);
                var candidates = actions.GetRange(skip, actions.Count - skip);

                if (candidates.Count == 0)
                    return [];

                var picks = Math.Clamp(policy.RandomPickCount, 1, candidates.Count);

                // A partial Fisher-Yates: only as many swaps as picks, each pick uniform over
                // what is left, which is what shuffling the whole list and taking its head gave.
                for (var i = 0; i < picks; i++)
                {
                    var j = Random.Shared.Next(i, candidates.Count);

                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }

                candidates.RemoveRange(picks, candidates.Count - picks);

                return candidates;
            }
            case WiredEffectModeType.Unseen:
            {
                _nextUnseenIndexByStackId.TryGetValue(stackId, out var index);

                var picked = actions[index % actions.Count];

                _nextUnseenIndexByStackId[stackId] = (index + 1) % actions.Count;

                return [picked];
            }
            default:
                return [.. actions];
        }
    }

    /// <summary>
    /// Sets a box or antenna's flash state. During a wired tick it waits for the next state update;
    /// otherwise it is sent at once.
    /// </summary>
    public Task SetFlashStateAsync(IRoomFloorItem item, int state)
    {
        if (!_collectingTickStates)
            return item.Logic.SetStateAsync(state);

        _pendingFloorStates.Add(item.ObjectId);

        return item.Logic.SetStateAsync(state, false);
    }

    // Sends each pending furni's state as it is now, so a change made outside wired since (a
    // player's double-click, already sent) is never overwritten by an older one.
    private void FlushPendingFloorStates()
    {
        if (_pendingFloorStates.Count == 0)
            return;

        List<(RoomObjectId, StuffDataSnapshot)> stuffDatas = [];

        foreach (var objectId in _pendingFloorStates)
        {
            if (FurniModule.TryGetFloorItem(objectId, out var item))
                stuffDatas.Add((objectId, item.Logic.StuffData.GetSnapshot()));
        }

        _pendingFloorStates.Clear();

        if (stuffDatas.Count == 0)
            return;

        _roomGrain.SendComposersToRoomAndForget([
            new ObjectsDataUpdateMessageComposer { StuffDatas = stuffDatas },
        ]);
    }

    private void Flash(IWiredBox box) =>
        box.FlashActivationStateAsync(CancellationToken.None)
            .LogAndForget(
                _roomGrain._logger,
                "flash a wired box in room {RoomId}",
                _roomGrain.RoomId
            );

    /// <summary>
    /// A signal flashes each antenna it is delivered to once (recorded on hhus,
    /// WIN63-202610121709: an antenna two "receive signal" triggers listen to flips once per signal).
    /// </summary>
    private void FlashAntennas(WiredSignalEvent signal)
    {
        foreach (var antennaId in signal.AntennaIds)
        {
            if (!FurniModule.TryGetFloorItem(antennaId, out var antenna))
                continue;

            SetFlashStateAsync(antenna, antenna.Logic.GetState() == 1 ? 0 : 1)
                .LogAndForget(
                    _roomGrain._logger,
                    "flash an antenna in room {RoomId}",
                    _roomGrain.RoomId
                );
        }
    }

    private static bool EvaluateConditions(
        List<IWiredCondition> conditions,
        WiredProcessingContext ctx
    )
    {
        if (conditions.Count == 0)
            return true;

        if (ctx.Policy.ConditionMode == WiredConditionModeType.None)
            return true;

        var matched = conditions.Count(c => c.Evaluate(ctx));
        var threshold = Math.Max(0, ctx.Policy.ConditionThreshold);

        return ctx.Policy.ConditionMode switch
        {
            WiredConditionModeType.Any => matched > 0,
            WiredConditionModeType.All => matched == conditions.Count,
            WiredConditionModeType.NoneMatch => matched == 0,
            WiredConditionModeType.NotAll => matched < conditions.Count,
            WiredConditionModeType.LessThan => matched < threshold,
            WiredConditionModeType.Exactly => matched == threshold,
            WiredConditionModeType.MoreThan => matched > threshold,
            _ => matched == conditions.Count,
        };
    }
}
