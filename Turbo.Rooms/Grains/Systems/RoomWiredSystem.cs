using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
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
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Runs the wired of a room. Boxes on one tile form a stack; an event that a stack trigger
/// accepts runs the selectors, addons and conditions of that stack and schedules its actions.
/// Signals, stack calls, periodic triggers and timers are driven from here as well.
/// </summary>
public sealed partial class RoomWiredSystem(RoomGrain roomGrain) : IRoomEventListener
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private readonly HashSet<int> _dirtyStackIds = [];
    private readonly Dictionary<int, IWiredStack> _stacksById = [];
    private readonly Dictionary<Type, List<int>> _stackIdsByEventType = [];
    private readonly Dictionary<int, int> _nextUnseenIndexByStackId = [];
    private readonly Queue<RoomEvent> _eventQueue = new();
    private readonly Dictionary<
        WiredExecutionKey,
        WiredPendingStackExecution
    > _pendingStackExecutions = [];
    private readonly PriorityQueue<(WiredExecutionKey key, long version), long> _stackSchedule =
        new();

    private int _tickMs => _roomGrain._wiredConfig.TickMs;
    private bool _firstRun = true;
    private long _nextStackExecutionId = 0;

    public async Task ProcessWiredAsync(long now, CancellationToken ct)
    {
        if (now < _roomGrain._state.NextWiredBoundaryMs)
            return;

        while (now >= _roomGrain._state.NextWiredBoundaryMs)
            _roomGrain._state.NextWiredBoundaryMs += _tickMs;

        RollExecutionWindow(now);

        if (_firstRun)
        {
            await ProcessInternalVariablesAsync(now, ct);

            _firstRun = false;
        }

        await ProcessVariableBoxesAsync(now, ct);
        await ProcessWiredStacksAsync(now, ct);
        await RunDueScheduledStackExecutionsAsync(now, ct);

        if (_stacksById.Count == 0)
        {
            _eventQueue.Clear();

            return;
        }

        await ProcessTimedTriggersAsync(now, ct);

        var budget = _roomGrain._wiredConfig.MaxEventsPerTick;

        while (budget-- > 0 && _eventQueue.Count > 0)
        {
            var evt = _eventQueue.Dequeue();

            await ProcessRoomEventAsync(evt, now, ct);
        }
    }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is null)
            return Task.CompletedTask;

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
                _eventQueue.Enqueue(evt);
                break;
            case RoomItemDetachedEvent detatchedEvt:
                _furnitureActiveStore.RemoveFurnitureStore(detatchedEvt.ObjectId);
                ForgetStoredValuesOfUnownedFurni(detatchedEvt.ObjectId);
                ForgetProjectileFlight(detatchedEvt.ObjectId);
                ForgetTimedTrigger(detatchedEvt.ObjectId);
                break;
            default:
                _eventQueue.Enqueue(evt);
                break;
        }

        return Task.CompletedTask;
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
        };

        SeedSelectionFromEvent(ctx, evt);

        var selection = ctx.GetSelection(trigger);

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
        foreach (var selector in ctx.Stack.Selectors)
            await ApplySelectorAsync(ctx, selector, ct);

        foreach (var addon in ctx.Stack.Addons)
        {
            if (!await addon.MutatePolicyAsync(ctx, ct))
                return;
        }

        var passed = EvaluateConditions(ctx.Stack.Conditions, ctx);

        if (ctx.Trigger is not null && !await ctx.Trigger.CanTriggerAsync(ctx, ct))
            return;

        List<IWiredAction> pool;

        if (negativeCall is bool negative)
        {
            if (negative == passed)
                return;

            pool = ctx.Stack.Actions.Where(x => !x.IsNegative).ToList();
        }
        else
        {
            pool = ctx.Stack.Actions.Where(x => x.IsNegative != passed).ToList();
        }

        if (pool.Count == 0)
            return;

        if (ctx.Trigger is not null)
            ctx.Trigger.FlashActivationStateAsync(ct)
                .LogAndForget(_roomGrain._logger, $"flash a wired box in room {_roomGrain.RoomId}");

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
        var furni = basis.SelectedFurniIds.Intersect(set.SelectedFurniIds).ToList();
        var players = basis.SelectedAvatarIds.Intersect(set.SelectedAvatarIds).ToList();

        ctx.SelectorPool.SelectedFurniIds.Clear();
        ctx.SelectorPool.SelectedAvatarIds.Clear();
        ctx.SelectorPool.SelectedFurniIds.UnionWith(furni);
        ctx.SelectorPool.SelectedAvatarIds.UnionWith(players);
    }

    private WiredSelectionSet InvertSelection(IWiredSelectionSet set)
    {
        var inverted = new WiredSelectionSet();

        foreach (var item in _roomGrain.FurniModule.Items)
        {
            if (!set.SelectedFurniIds.Contains(item.ObjectId))
                inverted.SelectedFurniIds.Add(item.ObjectId);
        }

        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
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
            case PlayerClickedAvatarEvent clickEvt:
                AddPlayerById(ctx, clickEvt.PlayerId);
                AddAvatarByObjectId(ctx, clickEvt.TargetObjectId);
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
        if (_roomGrain.AvatarModule.TryGetAvatar(objectId, out _))
            ctx.Selected.SelectedAvatarIds.Add(objectId);
    }

    /// <summary>An event names the player; a selection names the avatar they are here.</summary>
    private void AddPlayerById(WiredProcessingContext ctx, PlayerId playerId)
    {
        if (_roomGrain.AvatarModule.TryGetPlayer(playerId, out var player))
            ctx.Selected.SelectedAvatarIds.Add(player.ObjectId);
    }

    private async Task ProcessStackCallAsync(
        WiredStackCalledEvent evt,
        long now,
        CancellationToken ct
    )
    {
        if (evt.Depth > _roomGrain._wiredConfig.MaxDepth)
        {
            RecordError("WiredCallDepthExceeded", "Action 18", now);

            return;
        }

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
            };

            ctx.Selected.SelectedFurniIds.UnionWith(evt.FurniIds);
            ctx.Selected.SelectedAvatarIds.UnionWith(evt.AvatarIds);

            await RunStackAsync(ctx, now, evt.IsNegative, ct);
        }
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
            Policy = ctx.Policy,
            Selected = ctx.Selected,
            SelectorPool = ctx.SelectorPool,
            Signal = ctx.Signal,
            Depth = ctx.Depth,
            Version = 1,
            DueAtMs = dueAtMs + (long)ctx.Policy.Delay.TotalMilliseconds,
            NextActionIndex = 0,
        };

        _pendingStackExecutions[key] = pending;
        _stackSchedule.Enqueue((key, pending.Version), pending.DueAtMs);
    }

    private async Task RunDueScheduledStackExecutionsAsync(long now, CancellationToken ct)
    {
        var budget = _roomGrain._wiredConfig.MaxScheduledPerTick;
        var costCap = _roomGrain._wiredConfig.ExecutionCostCap;

        while (budget-- > 0 && _stackSchedule.Count > 0)
        {
            // A room that has spent its execution budget for the current cost window is
            // throttled: due executions stay queued and resume once the window rolls over.
            if (costCap > 0 && _executionsInWindow >= costCap)
                break;

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

            if (await ExecuteStackChainAsync(key, pending, now, ct))
                _pendingStackExecutions.Remove(key);
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
                    pending.WaitingActionIndex = i;

                    RescheduleStack(key, pending, now + delayMs);

                    return false;
                }
            }

            var succeeded = false;

            try
            {
                var ctx = new WiredExecutionContext(_roomGrain)
                {
                    Policy = pending.Policy,
                    Selected = new WiredSelectionSet().UnionWith(pending.Selected),
                    SelectorPool = new WiredSelectionSet().UnionWith(pending.SelectorPool),
                    Signal = new WiredSelectionSet().UnionWith(pending.Signal),
                    Depth = pending.Depth,
                    CancellationToken = ct,
                };

                action
                    .FlashActivationStateAsync(ct)
                    .LogAndForget(
                        _roomGrain._logger,
                        $"flash a wired box in room {_roomGrain.RoomId}"
                    );

                succeeded = await action.ExecuteAsync(ctx, ct);

                CountExecution();

                FlushWiredContextAsync(ctx)
                    .LogAndForget(
                        _roomGrain._logger,
                        $"flush wired results in room {_roomGrain.RoomId}"
                    );
            }
            catch (Exception ex)
            {
                RecordError(ex.GetType().Name, GetErrorCategory(action), now);

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
                return true;
        }

        return true;
    }

    private void RescheduleStack(
        WiredExecutionKey key,
        WiredPendingStackExecution pending,
        long dueAtMs
    )
    {
        if (pending.DueAtMs != dueAtMs)
            pending.Version++;

        pending.DueAtMs = dueAtMs;

        _pendingStackExecutions[key] = pending;
        _stackSchedule.Enqueue((key, pending.Version), pending.DueAtMs);
    }

    private Task FlushWiredContextAsync(WiredExecutionContext ctx)
    {
        if (
            ctx.UserMoves.Count > 0
            || ctx.UserDirections.Count > 0
            || ctx.FloorItemMoves.Count > 0
            || ctx.WallItemMoves.Count > 0
        )
            _roomGrain.SendComposerToRoomAndForget(
                new WiredMovementsMessageComposer
                {
                    Users = ctx.UserMoves,
                    FloorItems = ctx.FloorItemMoves,
                    WallItems = ctx.WallItemMoves,
                    UserDirections = ctx.UserDirections,
                }
            );

        if (ctx.FloorItemStateUpdates.Count > 0)
            _roomGrain.SendComposerToRoomAndForget(
                new ObjectsDataUpdateMessageComposer { StuffDatas = ctx.FloorItemStateUpdates }
            );

        if (ctx.WallItemStateUpdates.Count > 0)
            _roomGrain.SendComposerToRoomAndForget(
                new ItemsStateUpdateMessageComposer { ObjectStates = ctx.WallItemStateUpdates }
            );

        return Task.CompletedTask;
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

        foreach (var stack in _stacksById.Values)
        {
            foreach (var trigger in stack.Triggers)
            {
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
    }

    private async Task ProcessWiredStackAsync(int stackId, CancellationToken ct)
    {
        _stacksById.Remove(stackId);
        _nextUnseenIndexByStackId.Remove(stackId);

        var wiredItems = _roomGrain
            .FurniModule.GetFloorItemsOnTile(stackId)
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

                RecordError(ex.GetType().Name, GetErrorCategory(box), _roomGrain.NowMs());

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
                var candidates = actions.Skip(Math.Max(0, policy.RandomSkipCount)).ToList();

                if (candidates.Count == 0)
                    return [];

                var picks = Math.Clamp(policy.RandomPickCount, 1, candidates.Count);

                return candidates.OrderBy(_ => Random.Shared.Next()).Take(picks).ToList();
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
