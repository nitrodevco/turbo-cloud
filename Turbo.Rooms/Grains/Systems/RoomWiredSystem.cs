using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed class RoomWiredSystem(RoomGrain roomGrain) : IRoomEventListener
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private long _nextStackExecutionId = 0;

    private readonly HashSet<int> _dirtyStackIds = [];
    private readonly Dictionary<int, IWiredStack> _stacksById = [];
    private readonly Dictionary<Type, List<int>> _stackIdsByEventType = [];

    private readonly HashSet<int> _dirtyVariableBoxIds = [];
    private readonly Dictionary<int, string> _variableKeyBoxId = [];
    private readonly Dictionary<string, IWiredVariable> _variableByKey = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly Queue<RoomEvent> _eventQueue = new();
    private readonly Dictionary<
        WiredExecutionKey,
        WiredPendingStackExecution
    > _pendingStackExecutions = [];
    private readonly PriorityQueue<(WiredExecutionKey key, long version), long> _stackSchedule =
        new();

    private int _tickMs => _roomGrain._roomConfig.WiredTickMs;
    private bool _firstRun;

    public async Task ProcessWiredAsync(long now, CancellationToken ct)
    {
        if (now < _roomGrain._state.NextWiredBoundaryMs)
            return;

        while (now >= _roomGrain._state.NextWiredBoundaryMs)
            _roomGrain._state.NextWiredBoundaryMs += _tickMs;

        if (_firstRun)
        {
            var variables = _roomGrain._wiredVariablesProvider.BuildAllVariables(_roomGrain);

            foreach (var variable in variables)
                _variableByKey.Add(variable.VarDefinition.Key, variable);

            _firstRun = false;
        }

        await ProcessVariableBoxesAsync(now, ct);
        await ProcessWiredStacksAsync(now, ct);
        await RunDueScheduledStackExecutionsAsync(now, ct);

        if (_stacksById.Count == 0 || _stackIdsByEventType.Count == 0)
            return;

        var budget = _roomGrain._roomConfig.WiredMaxEventsPerTick;

        while (budget-- > 0 && _eventQueue.Count > 0)
        {
            var evt = _eventQueue.Dequeue();

            await ProcessRoomEventAsync(evt, now, ct);
        }
    }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not null)
        {
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
                default:
                    _eventQueue.Enqueue(evt);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    public bool TryGetVariable(
        string key,
        in IWiredVariableBinding binding,
        WiredExecutionContext ctx,
        out int value
    )
    {
        if (_variableByKey.TryGetValue(key, out var variable) && variable is not null)
            return variable.TryGet(binding, ctx, out value);

        value = 0;

        return false;
    }

    private async Task ProcessRoomEventAsync(RoomEvent evt, long now, CancellationToken ct)
    {
        if (evt is null || !_stackIdsByEventType.TryGetValue(evt.GetType(), out var stackIds))
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
        if (trigger is null || evt is null || stack is null)
            return;

        if (!await trigger.MatchesEventAsync(evt, ct))
            return;

        var ctx = new WiredProcessingContext
        {
            Room = _roomGrain,
            Event = evt,
            Stack = stack,
            Trigger = trigger,
        };

        var selection = await ctx.GetWiredSelectionSetAsync(trigger, ct);

        ctx.Selected.UnionWith(selection);

        foreach (var selector in ctx.Stack.Selectors)
        {
            var set = await selector.SelectAsync(ctx, ct);

            ctx.SelectorPool.UnionWith(set);
        }

        foreach (var addon in ctx.Stack.Addons)
            await addon.MutatePolicyAsync(ctx, ct);

        if (!EvaluateConditions(ctx.Stack.Conditions, ctx))
            return;

        if (!await trigger.CanTriggerAsync(ctx, ct))
            return;

        _ = ctx.Trigger.FlashActivationStateAsync(ct);

        foreach (var addon in ctx.Stack.Addons)
            await addon.BeforeEffectsAsync(ctx, ct);

        ScheduleStackExecution(ctx, now, ct);

        foreach (var addon in ctx.Stack.Addons)
            await addon.AfterEffectsAsync(ctx, ct);
    }

    private void ScheduleStackExecution(
        WiredProcessingContext ctx,
        long dueAtMs,
        CancellationToken ct
    )
    {
        var actions = ChooseActions(ctx.Stack.Actions, ctx.Policy);

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
            Variables = ctx.Variables,
            Policy = ctx.Policy,
            Selected = ctx.Selected,
            SelectorPool = ctx.SelectorPool,
            Version = 1,
            DueAtMs = dueAtMs,
            NextActionIndex = 0,
        };

        _pendingStackExecutions[key] = pending;
        _stackSchedule.Enqueue((key, pending.Version), pending.DueAtMs);
    }

    private async Task RunDueScheduledStackExecutionsAsync(long now, CancellationToken ct)
    {
        var budget = _roomGrain._roomConfig.WiredMaxScheduledPerTick;

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

            try
            {
                var ctx = new WiredExecutionContext
                {
                    Room = _roomGrain,
                    Variables = pending.Variables.ToDictionary(),
                    Policy = pending.Policy,
                    Selected = new WiredSelectionSet().UnionWith(pending.Selected),
                    SelectorPool = new WiredSelectionSet().UnionWith(pending.SelectorPool),
                };

                await action.ExecuteAsync(ctx, ct);

                _ = action.FlashActivationStateAsync(ct);

                FlushWiredMovementsForContext(ctx);
            }
            catch { }

            pending.NextActionIndex = i + 1;
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

    private void FlushWiredMovementsForContext(WiredExecutionContext ctx)
    {
        if (
            ctx.UserMoves.Count == 0
            && ctx.UserDirections.Count == 0
            && ctx.FloorItemMoves.Count == 0
            && ctx.WallItemMoves.Count == 0
        )
            return;

        _ = ctx.SendComposerToRoomAsync(
            new WiredMovementsMessageComposer
            {
                Users = ctx.UserMoves,
                FloorItems = ctx.FloorItemMoves,
                WallItems = ctx.WallItemMoves,
                UserDirections = ctx.UserDirections,
            }
        );
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

        var wiredItems = _roomGrain
            ._state.TileFloorStacks[stackId]
            .Select(x => _roomGrain._state.FloorItemsById[x])
            .Where(x =>
                x.Logic is FurnitureWiredLogic && x.Logic is not FurnitureWiredVariableLogic
            )
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
            catch (Exception)
            {
                continue;
            }
        }

        _stacksById[stackId] = stack;
    }

    private async Task ProcessVariableBoxesAsync(long now, CancellationToken ct)
    {
        if (_dirtyVariableBoxIds.Count == 0)
            return;

        var dirtyVariableBoxIds = _dirtyVariableBoxIds.ToList();
        _dirtyVariableBoxIds.Clear();

        foreach (var boxId in dirtyVariableBoxIds)
            await ProcessVariableBoxAsync(boxId, ct);

        long globalHash = 0;

        foreach (var variable in _variableByKey.Values)
            globalHash ^= variable.GetHashCode();

        _roomGrain._state.GlobalVariableHash = (int)globalHash;
    }

    private async Task ProcessVariableBoxAsync(int boxId, CancellationToken ct)
    {
        RemoveVariableBox(boxId);

        if (
            !_roomGrain._state.FloorItemsById.TryGetValue(boxId, out var floorItem)
            || floorItem.Logic is not FurnitureWiredVariableLogic varLogic
        )
            return;

        await varLogic.LoadWiredAsync(ct);

        var key = varLogic.VarDefinition.Key;

        if (string.IsNullOrWhiteSpace(key))
            return;

        _variableByKey[key] = varLogic;
        _variableKeyBoxId[boxId] = key;
    }

    private void RemoveVariableBox(int boxId)
    {
        if (!_variableKeyBoxId.TryGetValue(boxId, out var key))
            return;

        _variableByKey.Remove(key);
        _variableKeyBoxId.Remove(boxId);
    }

    private static List<IWiredAction> ChooseActions(List<IWiredAction> actions, IWiredPolicy policy)
    {
        if (actions.Count == 0)
            return [];

        return policy.EffectMode switch
        {
            EffectModeType.FirstOnly => [actions[0]],
            EffectModeType.Random => [actions[Random.Shared.Next(actions.Count)]],
            _ => [.. actions],
        };
    }

    private static bool EvaluateConditions(
        List<IWiredCondition> conditions,
        WiredProcessingContext ctx
    )
    {
        if (conditions.Count == 0)
            return true;

        return ctx.Policy.ConditionMode switch
        {
            ConditionModeType.None => true,
            ConditionModeType.Any => conditions.Exists(c => c.Evaluate(ctx)),
            ConditionModeType.All => conditions.TrueForAll(c => c.Evaluate(ctx)),
            _ => conditions.TrueForAll(c => c.Evaluate(ctx)),
        };
    }
}
