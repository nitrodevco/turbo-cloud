using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

internal sealed class RoomWiredSystem(
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

    private WiredCompiled? _wiredCompiled = null;
    private Dictionary<int, WiredStack> _stacksById = [];

    private readonly HashSet<int> _dirtyStackIds = [];
    private readonly Queue<RoomEvent> _queue = new();

    private bool _isProcessingQueue = false;

    private int _tickMs => _roomConfig.WiredTickMs;

    public async Task ProcessWiredAsync(long now, CancellationToken ct)
    {
        if (now < _state.NextWiredBoundaryMs)
            return;

        while (now >= _state.NextWiredBoundaryMs)
            _state.NextWiredBoundaryMs += _tickMs;

        if (_stacksById.Count == 0)
            return;

        while (_queue.Count > 0)
        {
            var payload = _queue.Dequeue();

            await ProcessRoomEventAsync(payload, ct);
        }
    }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct) =>
        ProcessRoomEventAsync(evt, ct);

    private async Task ProcessRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is null)
            return;

        if (evt is RoomWiredStackChangedEvent stackChanged)
        {
            foreach (var stackId in stackChanged.StackIds)
                _dirtyStackIds.Add(stackId);

            return;
        }

        await CompileLatestWiredAsync(ct);

        if (_wiredCompiled is null)
            return;

        if (!_wiredCompiled.StackIdsByEventType.TryGetValue(evt.GetType(), out var stackIds))
            return;

        var wiredContexts = new List<IWiredContext>();

        foreach (var stackId in stackIds)
        {
            var stack = _stacksById[stackId];

            if (stack is null)
                continue;

            foreach (var trigger in stack.Triggers)
            {
                if (await trigger.MatchesEventAsync(evt, ct))
                {
                    var ctx = new WiredContext
                    {
                        Room = _roomGrain,
                        Event = evt,
                        Stack = stack,
                        Trigger = trigger,
                    };

                    wiredContexts.Add(ctx);

                    if (ctx.Policy.ShortCircuitOnFirstEffectSuccess)
                        break;
                }
            }
        }

        if (wiredContexts.Count == 0)
            return;

        foreach (var ctx in wiredContexts)
            await ProcessWiredContextAsync(ctx, ct);
    }

    private async Task ProcessWiredContextAsync(IWiredContext ctx, CancellationToken ct)
    {
        if (ctx.Trigger is not null)
        {
            var selection = await ctx.GetWiredSelectionSetAsync(ctx.Trigger, ct);

            ctx.Selected.UnionWith(selection);
        }

        foreach (var selector in ctx.Stack.Selectors)
        {
            var produced = await selector.SelectAsync(ctx, ct);

            ctx.SelectorPool.SelectedFurniIds.UnionWith(produced.SelectedFurniIds);
            ctx.SelectorPool.SelectedAvatarIds.UnionWith(produced.SelectedAvatarIds);
        }

        foreach (var variable in ctx.Stack.Variables)
            await variable.ApplyAsync(ctx, ct);

        foreach (var addon in ctx.Stack.Addons)
            await addon.MutatePolicyAsync(ctx, ct);

        if (!EvaluateConditions(ctx.Stack.Conditions, ctx))
            return;

        if (ctx.Trigger is not null)
        {
            if (!await ctx.Trigger.CanTriggerAsync(ctx, ct))
                return;

            _ = ctx.Trigger.FlashActivationStateAsync();
        }

        foreach (var addon in ctx.Stack.Addons)
            await addon.BeforeEffectsAsync(ctx, ct);

        if (ctx.Policy.Delay is { } delay)
            await Task.Delay(delay, ct);

        await ExecuteEffectsAsync(ctx.Stack.Effects, ctx, ct);

        foreach (var addon in ctx.Stack.Addons)
            await addon.AfterEffectsAsync(ctx, ct);
    }

    private static bool EvaluateConditions(List<IWiredCondition> conditions, IWiredContext ctx)
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

    private static async Task ExecuteEffectsAsync(
        List<IWiredAction> effects,
        IWiredContext ctx,
        CancellationToken ct
    )
    {
        if (effects.Count == 0)
            return;

        switch (ctx.Policy.EffectMode)
        {
            case EffectModeType.FirstOnly:
            {
                await effects[0].ExecuteAsync(ctx, ct);
                return;
            }

            case EffectModeType.Random:
            {
                var idx = Random.Shared.Next(effects.Count);

                await effects[idx].ExecuteAsync(ctx, ct);

                return;
            }

            default:
            case EffectModeType.All:
            {
                foreach (var effect in effects)
                {
                    var ok = await effect.ExecuteAsync(ctx, ct);

                    if (ok && ctx.Policy.ShortCircuitOnFirstEffectSuccess)
                        break;
                }

                return;
            }
        }
    }

    private async Task CompileLatestWiredAsync(CancellationToken ct)
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
                var eventTypes = trigger.SupportedEventTypes;

                foreach (var eventType in eventTypes)
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
            .Where(x => x.Logic is IFurnitureWiredLogic)
            .ToList();

        if (wiredItems.Count == 0)
            return null;

        var stack = new WiredStack { StackId = stackId };

        foreach (var item in wiredItems)
        {
            try
            {
                var wiredLogic = (IFurnitureWiredLogic)item.Logic!;

                await wiredLogic.LoadWiredAsync(ct);

                switch (wiredLogic)
                {
                    case IWiredTrigger trigger:
                        stack.Triggers.Add(trigger);
                        break;
                    case IWiredSelector selector:
                        stack.Selectors.Add(selector);
                        break;
                    case IWiredCondition condition:
                        stack.Conditions.Add(condition);
                        break;
                    case IWiredAddon addon:
                        stack.Addons.Add(addon);
                        break;
                    case IWiredVariable variable:
                        stack.Variables.Add(variable);
                        break;
                    case IWiredAction effect:
                        stack.Effects.Add(effect);
                        break;
                }
            }
            catch (Exception)
            {
                continue;
            }

            continue;
        }

        _stacksById[stackId] = stack;

        return stack;
    }
}
