using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// Compares the picked variable on the selected targets with a literal or another variable.
/// Params: target, comparison, operand mode, operand (hi, lo), operand target, and since
/// WIN63-202610121624 "same holder" (AS3 <c>VariableValue.readIntParamsFromForm</c>: each target
/// is compared with the operand variable it holds itself).
/// </summary>
[RoomObjectLogic("wf_cnd_var_val_match")]
public class WiredConditionVariableValueMatch(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.VARIABLE_VALUE;

    protected override bool AllowsWallFurni => true;

    // "Match condition if: All match / Any match" (official client capture: quantifierType 3;
    // with values 20 and 5 against ">= 10", all blocks and any passes).
    protected override WiredQuantifierType QuantifierType => WiredQuantifierType.Variables;

    public override int GetMaxVariableIds() => 2;

    // The variable and its reference by position: the same variable may be both (official client
    // capture: Change Variable saved with ids [mark, mark]).
    protected override bool HasPositionalVariableIds => true;

    // A fresh box compares "more than" against a global reference (official client capture:
    // defaultIntParams [1,2,0,0,0,-10,0]).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.GreaterThan),
            new WiredBoolParamRule(false),
            WiredRules.AnyInt(),
            WiredRules.AnyInt(),
            WiredRules.VariableTarget(WiredVariableTargetType.Global),
            new WiredBoolParamRule(false),
        ];

    private const int PARAM_SAME_HOLDER = 6;

    protected override int GetRequiredIntParamCount() => PARAM_SAME_HOLDER;

    // Two merged selections, the holders acted on and the reference holders, each its own furni
    // and user slot (official client capture of the box's sources).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectorItems),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users.DefaultingTo(WiredPlayerSourceType.SelectorUsers)];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var variable = GetVariable(0);

        if (variable is null)
            return false;

        var selection = GetMergedSelection(ctx, 0);
        var sameHolder =
            GetIntParamOrDefault(2, false) && GetIntParamOrDefault(PARAM_SAME_HOLDER, false);
        long operand = 0;

        if (!sameHolder && !TryResolveOperand(2, 3, 5, 1, GetMergedSelection(ctx, 1), out operand))
            return false;

        var targetType = GetTargetType(variable, 0);
        var comparison = GetIntParamOrDefault(1, WiredComparisonType.Equals);
        var targets = GetTargetIds(targetType, selection).ToList();

        return Quantify(
            targets.Select(id =>
            {
                var own = operand;

                return ReadVariable(variable, targetType, id) is { } value
                    && (!sameHolder || TryReadOperandOnHolder(1, targetType, id, out own))
                    && WiredComparison.Compare(comparison, value, own);
            }),
            QuantifierRequiresAll
        );
    }
}
