using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed class RoomWiredSystem(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomAvatarModule roomAvatarModule,
    RoomMapModule roomMapModule
) : IRoomEventListener
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomAvatarModule _roomAvatarModule = roomAvatarModule;
    private readonly RoomMapModule _roomMapModule = roomMapModule;

    private WiredCompiled? _wiredCompiled;
    private long _nextStackExecutionId = 0;

    private readonly HashSet<int> _dirtyStackIds = [];
    private readonly Dictionary<int, WiredStack> _stacksById = [];
    private readonly Queue<RoomEvent> _eventQueue = new();
    private readonly Dictionary<
        WiredExecutionKey,
        WiredPendingStackExecution
    > _pendingStackExecutions = [];
    private readonly PriorityQueue<(WiredExecutionKey key, long version), long> _stackSchedule =
        new();

    private int _tickMs => _roomConfig.WiredTickMs;

    public async Task ProcessWiredAsync(long now, CancellationToken ct)
    {
        if (now < _state.NextWiredBoundaryMs)
            return;

        while (now >= _state.NextWiredBoundaryMs)
            _state.NextWiredBoundaryMs += _tickMs;

        await CompileLatestWiredAsync(now, ct);
        await RunDueScheduledStackExecutionsAsync(now, ct);

        if (_wiredCompiled is null || _wiredCompiled.StackIdsByEventType.Count == 0)
            return;

        var budget = _roomConfig.WiredMaxEventsPerTick;

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
            if (evt is RoomWiredStackChangedEvent stackChanged)
            {
                foreach (var stackId in stackChanged.StackIds)
                    _dirtyStackIds.Add(stackId);
            }
            else
            {
                _eventQueue.Enqueue(evt);
            }
        }

        return Task.CompletedTask;
    }

    private async Task ProcessRoomEventAsync(RoomEvent evt, long now, CancellationToken ct)
    {
        if (
            evt is null
            || _wiredCompiled is null
            || !_wiredCompiled.StackIdsByEventType.TryGetValue(evt.GetType(), out var stackIds)
        )
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
        FurnitureWiredTriggerLogic trigger,
        RoomEvent evt,
        WiredStack stack,
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

        foreach (var variable in ctx.Stack.Variables)
            await variable.ApplyAsync(ctx, ct);

        foreach (var addon in ctx.Stack.Addons)
            await addon.MutatePolicyAsync(ctx, ct);

        if (!EvaluateConditions(ctx.Stack.Conditions, ctx))
            return;

        if (!await trigger.CanTriggerAsync(ctx, ct))
            return;

        _ = ctx.Trigger.FlashActivationStateAsync();

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
        var actions = ChooseActions(ctx.Stack.Effects, ctx.Policy);

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
        var budget = _roomConfig.WiredMaxScheduledPerTick;

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

    private async Task CompileLatestWiredAsync(long now, CancellationToken ct)
    {
        if (_dirtyStackIds.Count == 0 && _wiredCompiled is not null)
            return;

        if (_dirtyStackIds.Count > 0)
        {
            var dirtyStackIds = _dirtyStackIds.ToList();
            _dirtyStackIds.Clear();

            foreach (var stackId in dirtyStackIds)
                await ComputeWiredStackAsync(stackId, ct);
        }

        var compile = new WiredCompiled();

        foreach (var (key, value) in _stacksById)
            compile.StacksById[key] = value;

        foreach (var (key, value) in compile.StacksById)
        {
            foreach (var trigger in value.Triggers)
            {
                foreach (var eventType in trigger.SupportedEventTypes)
                {
                    if (!compile.StackIdsByEventType.TryGetValue(eventType, out var list))
                    {
                        list = [];
                        compile.StackIdsByEventType[eventType] = list;
                    }

                    list.Add(key);
                }
            }
        }

        foreach (var list in compile.StackIdsByEventType.Values)
            list.Sort();

        _wiredCompiled = compile;
    }

    private async Task<WiredStack?> ComputeWiredStackAsync(int stackId, CancellationToken ct)
    {
        _stacksById.Remove(stackId);

        var wiredItems = _state
            .TileFloorStacks[stackId]
            .Select(x => _state.FloorItemsById[x])
            .Where(x => x.Logic is FurnitureWiredLogic)
            .ToList();

        if (wiredItems.Count == 0)
            return null;

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
                    case FurnitureWiredVariableLogic variable:
                        stack.Variables.Add(variable);
                        break;
                    case FurnitureWiredActionLogic effect:
                        stack.Effects.Add(effect);
                        break;
                }
            }
            catch (Exception)
            {
                continue;
            }
        }

        _stacksById[stackId] = stack;
        return stack;
    }

    private static List<FurnitureWiredActionLogic> ChooseActions(
        List<FurnitureWiredActionLogic> actions,
        WiredPolicy policy
    )
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
        List<FurnitureWiredConditionLogic> conditions,
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
