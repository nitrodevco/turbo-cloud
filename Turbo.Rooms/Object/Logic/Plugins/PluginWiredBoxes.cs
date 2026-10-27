using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Plugins;

// The four hosts below are core's own kinds with the plugin's declaration read in. Each repeats
// the same seven overrides because each derives from a different base; what they do is in
// FurnitureWiredLogic.Plugins.cs.

/// <summary>A plugin's <see cref="WiredEffect"/> run as an effect of core's.</summary>
internal sealed class PluginWiredEffectLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx,
    WiredEffect box
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx), IPluginWiredHost
{
    public WiredBox Box => box;

    public override int WiredCode => box.EditorCode;

    public override List<IWiredParamRule> GetIntParamRules() => PluginRules(box);

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        PluginFurniSources(box);

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        PluginPlayerSources(box);

    protected override int GetStringParamMaxLength() => PluginTextLimit(box);

    protected override bool GetValidStuffIds(List<int> proposed, out List<int> stuffIds) =>
        base.GetValidStuffIds(proposed, out stuffIds) && PluginValidStuffIds(box, stuffIds);

    protected override WiredDataSnapshot BuildSnapshot() =>
        base.BuildSnapshot() with
        {
            FurniLimit = PluginFurniLimit(box),
        };

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        box.UsesTextPlaceholders ? TextPlaceholdersContext() : [];

    // The placeholders come from the other boxes of the stack, which change without this one
    // being saved: read them again each time the editor opens, as Show Message does.
    public override Task<bool> RefreshEditorContextAsync(CancellationToken ct) =>
        box.UsesTextPlaceholders ? Task.FromResult(true) : base.RefreshEditorContextAsync(ct);

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct) =>
        box.ExecuteAsync(PluginContext(box, ctx, ctx), ct);
}

/// <summary>A plugin's <see cref="WiredCondition"/> run as a condition of core's (negation and invert are core's).</summary>
internal sealed class PluginWiredConditionLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx,
    WiredCondition box
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx), IPluginWiredHost
{
    public WiredBox Box => box;

    public override int WiredCode => box.EditorCode;

    public override List<IWiredParamRule> GetIntParamRules() => PluginRules(box);

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        PluginFurniSources(box);

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        PluginPlayerSources(box);

    protected override int GetStringParamMaxLength() => PluginTextLimit(box);

    protected override bool GetValidStuffIds(List<int> proposed, out List<int> stuffIds) =>
        base.GetValidStuffIds(proposed, out stuffIds) && PluginValidStuffIds(box, stuffIds);

    protected override WiredDataSnapshot BuildSnapshot() =>
        base.BuildSnapshot() with
        {
            FurniLimit = PluginFurniLimit(box),
        };

    protected override bool EvaluateCore(IWiredProcessingContext ctx) =>
        box.Evaluate(PluginContext(box, ctx, null));
}

/// <summary>A plugin's <see cref="WiredSelector"/> run as a selector of core's (filter and invert are core's).</summary>
internal sealed class PluginWiredSelectorLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx,
    WiredSelector box
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx), IPluginWiredHost
{
    public WiredBox Box => box;

    public override int WiredCode => box.EditorCode;

    public override bool SelectsFurni => !box.SelectsUsers;

    public override bool SelectsUsers => box.SelectsUsers;

    public override List<IWiredParamRule> GetIntParamRules() => PluginRules(box);

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        PluginFurniSources(box);

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        PluginPlayerSources(box);

    protected override int GetStringParamMaxLength() => PluginTextLimit(box);

    protected override bool GetValidStuffIds(List<int> proposed, out List<int> stuffIds) =>
        base.GetValidStuffIds(proposed, out stuffIds) && PluginValidStuffIds(box, stuffIds);

    protected override WiredDataSnapshot BuildSnapshot() =>
        base.BuildSnapshot() with
        {
            FurniLimit = PluginFurniLimit(box),
        };

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        WiredSelection picked;

        // A plugin's mistake selects nothing rather than stopping the room's wired tick.
        try
        {
            picked = box.Select(PluginContext(box, ctx, null));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Plugin wired selector {Box} failed in room {RoomId}",
                box.GetType().Name,
                _ctx.RoomId
            );

            return Task.FromResult<IWiredSelectionSet>(new WiredSelectionSet());
        }

        // Only what is in the room: a plugin cannot select a furni or a user that is not here.
        return Task.FromResult<IWiredSelectionSet>(
            new WiredSelectionSet(
                box.SelectsUsers ? [] : picked.Furni.Where(id => FurniModule.TryGetItem(id, out _)),
                box.SelectsUsers
                    ? picked
                        .Users.Where(index => AvatarModule.TryGetAvatar(index, out _))
                        .Select(index => (Primitives.Rooms.Object.RoomObjectId)index)
                    : []
            )
        );
    }
}

/// <summary>
/// A plugin's <see cref="WiredTrigger"/> run as a trigger of core's: it hears the plugin events
/// (<see cref="PluginWiredEvent"/>) of its event type, read back from their JSON.
/// </summary>
internal sealed class PluginWiredTriggerLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx,
    WiredTrigger box
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx), IPluginWiredHost
{
    private readonly string _eventKey = WiredEvents.KeyOf(box.EventType);

    public WiredBox Box => box;

    public override int WiredCode => box.EditorCode;

    public override List<Type> SupportedEventTypes { get; } = [typeof(PluginWiredEvent)];

    public override List<IWiredParamRule> GetIntParamRules() => PluginRules(box);

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        PluginFurniSources(box);

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        PluginPlayerSources(box);

    protected override int GetStringParamMaxLength() => PluginTextLimit(box);

    protected override bool GetValidStuffIds(List<int> proposed, out List<int> stuffIds) =>
        base.GetValidStuffIds(proposed, out stuffIds) && PluginValidStuffIds(box, stuffIds);

    protected override WiredDataSnapshot BuildSnapshot() =>
        base.BuildSnapshot() with
        {
            FurniLimit = PluginFurniLimit(box),
        };

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is PluginWiredEvent fired && fired.EventKey == _eventKey);

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not PluginWiredEvent fired || fired.EventKey != _eventKey)
            return Task.FromResult(false);

        // A plugin's mistake does not fire the stack rather than stopping the room's wired tick.
        try
        {
            return Task.FromResult(
                JsonSerializer.Deserialize(fired.Payload, box.EventType) is { } payload
                    && box.MatchesEvent(payload, PluginConfig(box))
            );
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Plugin wired trigger {Box} failed in room {RoomId}",
                box.GetType().Name,
                _ctx.RoomId
            );

            return Task.FromResult(false);
        }
    }
}
