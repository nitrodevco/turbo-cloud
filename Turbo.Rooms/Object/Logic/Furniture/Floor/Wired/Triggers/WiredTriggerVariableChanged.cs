using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when the picked variable is created, written or removed on any target. Int params:
/// the variable target, then a bitmask of the change kinds to react to (bit 0 created, bit 1
/// updated, bit 2 removed; zero means all). The target the change happened on becomes the
/// triggering furni or user.
/// </summary>
[RoomObjectLogic("wf_trg_var_changed")]
public class WiredTriggerVariableChanged(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.VARIABLE_UPDATE;
    public override List<Type> SupportedEventTypes { get; } = [typeof(WiredVariableChangedEvent)];

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [WiredRules.VariableTarget(WiredVariableTargetType.User), WiredRules.AnyInt()];

    public override IWiredParamRule? GetIntParamTailRule() => WiredRules.AnyInt();

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not WiredVariableChangedEvent change)
            return Task.FromResult(false);

        var variable = GetVariable(0);

        if (variable is null || variable.GetVarSnapshot().VariableId != change.VariableId)
            return Task.FromResult(false);

        var mask = GetIntParamOrDefault(1, 0);

        if (mask == 0)
            return Task.FromResult(true);

        var bit = 1 << (int)change.ChangeType;

        return Task.FromResult((mask & bit) != 0);
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not WiredVariableChangedEvent change)
            return Task.FromResult(false);

        switch (change.TargetType)
        {
            case WiredVariableTargetType.Furni:
                ctx.Selected.SelectedFurniIds.Add(change.TargetId);
                break;
            case WiredVariableTargetType.User:
                ctx.Selected.SelectedAvatarIds.Add(change.TargetId);
                break;
        }

        return Task.FromResult(true);
    }
}
