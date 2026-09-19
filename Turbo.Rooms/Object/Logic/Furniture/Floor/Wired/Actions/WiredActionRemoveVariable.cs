using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Removes the picked variable from the selected targets. Param 0 is the target type.</summary>
[RoomObjectLogic("wf_act_remove_var")]
public class WiredActionRemoveVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.REMOVE_VARIABLE;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [WiredRules.VariableTarget(WiredVariableTargetType.User)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var variable = GetVariable(0);

        if (variable is null)
            return Task.FromResult(false);

        var targetType = GetTargetType(variable, 0);
        var selection = ctx.GetSelection(this);
        var removed = false;

        foreach (var targetId in GetTargetIds(targetType, selection))
        {
            removed |= variable.RemoveValue(
                new WiredVariableKey(variable.GetVarSnapshot().VariableId, targetType, targetId)
            );
        }

        return Task.FromResult(removed);
    }
}
