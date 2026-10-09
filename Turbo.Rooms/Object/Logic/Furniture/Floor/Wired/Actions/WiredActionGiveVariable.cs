using System;
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

/// <summary>
/// Gives the selected users, furni or the running stack a variable (Flash
/// <c>actiontypes._-92n</c>). Int params as its editor writes them: the target, the initial value
/// as a long (<c>Util.pushIntAsLong</c>: the high word, -1 for a negative value, then the value)
/// and whether a holder's existing value is overridden.
/// </summary>
[RoomObjectLogic("wf_act_give_var")]
public class WiredActionGiveVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_TARGET = 0;
    private const int PARAM_INITIAL_VALUE = 1;
    private const int PARAM_OVERRIDE = 3;

    public override int WiredCode => (int)WiredActionType.GIVE_VARIABLE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredVariableTargetType>(
                WiredVariableTargetType.User,
                WiredVariableTargetType.User,
                WiredVariableTargetType.Furni,
                WiredVariableTargetType.Context
            ),
            // The long's high word: 0, or -1 for a negative initial value.
            new WiredRangeParamRule(-1, 0, 0),
            WiredRules.AnyInt(),
            new WiredBoolParamRule(false),
        ];

    public override int GetMaxVariableIds() => 1;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var targetType = GetIntParamOrDefault(PARAM_TARGET, WiredVariableTargetType.User);
        var value = new WiredVariableValue(GetLongParam(PARAM_INITIAL_VALUE));
        var replace = GetIntParamOrDefault(PARAM_OVERRIDE, false);
        var given = false;

        foreach (var variableId in _wiredData.VariableIds)
        {
            if (
                !WiredVariableId.TryParse(variableId, out var id)
                || WiredSystem.GetVariableById(id) is not { } variable
            )
                continue;

            // A user variable is keyed by the avatar, not by the player; a context variable has
            // one holder, the wired execution running this box.
            var targetIds =
                targetType == WiredVariableTargetType.Context
                    ? [0]
                    : GetTargetIds(targetType, selection);

            foreach (var targetId in targetIds)
                given |= await variable.GiveValueAsync(
                    new WiredVariableKey(id, targetType, targetId),
                    value,
                    replace
                );
        }

        return given;
    }
}
