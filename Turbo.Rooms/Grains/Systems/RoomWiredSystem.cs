using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

internal sealed class RoomWiredSystem(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomAvatarModule roomAvatarModule,
    RoomMapModule roomMapModule,
    IWiredDefinitionProvider wiredDefinitionProvider
) : IRoomEventListener
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomAvatarModule _roomAvatar = roomAvatarModule;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly IWiredDefinitionProvider _wiredDefinitionProvider = wiredDefinitionProvider;

    private WiredCompiled? _wiredCompiled;
    private Dictionary<int, WiredProgramStack> _stacksById = [];
    private HashSet<int> _dirtyStackIds = [];

    private int _tickMs => _roomConfig.WiredTickMs;

    public async Task ProcessWiredAsync(long now, CancellationToken ct)
    {
        if (now < _state.NextWiredBoundaryMs)
            return;

        while (now >= _state.NextWiredBoundaryMs)
            _state.NextWiredBoundaryMs += _tickMs;

        ComputeWiredStacks();

        var compiled = _wiredCompiled;
    }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct) =>
        HandleRoomEventAsync(evt, ct);

    private async Task HandleRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is null)
            return;

        if (evt is RoomWiredStackChangedEvent stackChanged)
        {
            foreach (var stackId in stackChanged.StackIds)
                _dirtyStackIds.Add(stackId);

            return;
        }

        if (
            _wiredCompiled is null
            || !_wiredCompiled.StackIdsByEventType.TryGetValue(evt.GetType(), out var stackIds)
        )
            return;

        foreach (var stackId in stackIds)
        {
            var program = _wiredCompiled.StacksById[stackId];

            var ctx = new WiredContext { Room = _roomGrain, Event = evt };

            await ProcessWiredProgramAsync(program, ctx, ct);
        }
    }

    private async Task ProcessWiredProgramAsync(
        WiredProgramStack stack,
        IWiredContext ctx,
        CancellationToken ct
    )
    {
        if (++ctx.Depth > _roomConfig.WiredMaxDepth)
            return;

        foreach (var selector in stack.Selectors)
            selector.Select(ctx);

        var fired = false;

        foreach (var trigger in stack.Triggers)
        {
            if (await trigger.MatchesAsync(ctx))
            {
                fired = true;
                break;
            }
        }

        if (!fired)
            return;

        foreach (var variable in stack.Variables)
            variable.Apply(ctx);

        foreach (var addon in stack.Addons)
            await addon.MutatePolicyAsync(ctx, ct);

        if (!EvaluateConditions(stack.Conditions, ctx))
            return;

        foreach (var addon in stack.Addons)
            await addon.BeforeEffectsAsync(ctx, ct);

        if (ctx.Policy.Delay is { } delay)
            await Task.Delay(delay, ct);

        await ExecuteEffectsAsync(stack.Effects, ctx, ct);

        foreach (var addon in stack.Addons)
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
        List<IWiredEffect> effects,
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

    private void ComputeWiredStacks()
    {
        if (_dirtyStackIds.Count == 0 || !_state.IsFurniLoaded)
            return;

        var compiled = new WiredCompiled();
        var dirtyStackIds = _dirtyStackIds.ToList();

        _dirtyStackIds.Clear();

        foreach (var stackId in dirtyStackIds)
        {
            _stacksById.Remove(stackId);

            var wiredItems = _state
                .TileFloorStacks[stackId]
                .Select(x => _state.FloorItemsById[x])
                .Where(x => x.Logic is FurnitureWiredLogic)
                .ToList();

            if (wiredItems.Count == 0)
                continue;

            var program = new WiredProgramStack { StackId = stackId };

            foreach (var item in wiredItems)
            {
                try
                {
                    var logic = (FurnitureWiredLogic)item.Logic;
                    var wiredDef = logic.WiredDefinition;

                    if (wiredDef is null)
                        continue;

                    switch (wiredDef)
                    {
                        case IWiredTrigger trigger:
                            program.Triggers.Add(trigger);
                            break;
                        case IWiredSelector selector:
                            program.Selectors.Add(selector);
                            break;
                        case IWiredCondition condition:
                            program.Conditions.Add(condition);
                            break;
                        case IWiredAddon addon:
                            program.Addons.Add(addon);
                            break;
                        case IWiredVariable variable:
                            program.Variables.Add(variable);
                            break;
                        case IWiredEffect effect:
                            program.Effects.Add(effect);
                            break;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            compiled.StacksById[stackId] = program;

            foreach (var trigger in program.Triggers)
            {
                var eventTypes = trigger.SupportedEventTypes;

                foreach (var eventType in eventTypes)
                {
                    if (!compiled.StackIdsByEventType.TryGetValue(eventType, out var list))
                    {
                        list = [];
                        compiled.StackIdsByEventType[eventType] = list;
                    }

                    list.Add(stackId);
                }
            }
        }

        foreach (var list in compiled.StackIdsByEventType.Values)
            list.Sort();

        _wiredCompiled = compiled;
    }
}
