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
/// <c>actiontypes.GiveVariable</c>). Int params as its editor writes them: the target, the initial
/// value as a long (<c>Util.pushIntAsLong</c>: the high word, -1 for a negative value, then the
/// value) and whether a holder's existing value is overridden; since WIN63-202610121624 the
/// initial value may come from a variable (Wired Faculty 12/10/2026: "Give Variable can
/// initialize from a variable"): whether it does, that variable's target, and "same holder"
/// (read on each target itself). Variable ids: the variable, then the reference variable.
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
    private const int PARAM_FROM_VARIABLE = 4;
    private const int PARAM_REFERENCE_TARGET = 5;
    private const int PARAM_SAME_HOLDER = 6;

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
            new WiredBoolParamRule(false),
            // A fresh box's reference is a global variable (official client capture,
            // WIN63-202610121709: open payload defaults with -10 here).
            WiredRules.VariableTarget(WiredVariableTargetType.Global),
            new WiredBoolParamRule(false),
        ];

    protected override int GetRequiredIntParamCount() => PARAM_FROM_VARIABLE;

    public override int GetMaxVariableIds() => 2;

    // The variable and its reference by position: the same variable may be both (official client
    // capture: Change Variable saved with ids [mark, mark]).
    protected override bool HasPositionalVariableIds => true;

    // Two merged selections, the holders given to and the reference holders, each its own furni
    // and user slot (mergedSelections [[0,0],[1,1]]; official client capture of the box's sources).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectorItems),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users.DefaultingTo(WiredPlayerSourceType.SelectorUsers)];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = GetMergedSelection(ctx, 0);
        var targetType = GetIntParamOrDefault(PARAM_TARGET, WiredVariableTargetType.User);
        var replace = GetIntParamOrDefault(PARAM_OVERRIDE, false);
        var fromVariable = GetIntParamOrDefault(PARAM_FROM_VARIABLE, false);
        var sameHolder = fromVariable && GetIntParamOrDefault(PARAM_SAME_HOLDER, false);
        var initial = GetLongParam(PARAM_INITIAL_VALUE);
        var given = false;

        if (GetVariable(0) is not { } variable)
            return false;

        if (
            fromVariable
            && !sameHolder
            && !TryReadVariableOperand(
                1,
                PARAM_REFERENCE_TARGET,
                GetMergedSelection(ctx, 1),
                out initial
            )
        )
            return false;

        var id = variable.GetVarSnapshot().VariableId;

        // A user variable is keyed by the avatar, not by the player; a context variable has
        // one holder, the wired execution running this box.
        var targetIds =
            targetType == WiredVariableTargetType.Context
                ? [0]
                : GetTargetIds(targetType, selection);

        foreach (var targetId in targetIds)
        {
            var value = initial;

            if (sameHolder && !TryReadOperandOnHolder(1, targetType, targetId, out value))
                continue;

            given |= await variable.GiveValueAsync(
                new WiredVariableKey(id, targetType, targetId),
                new WiredVariableValue(value),
                replace
            );
        }

        return given;
    }
}
