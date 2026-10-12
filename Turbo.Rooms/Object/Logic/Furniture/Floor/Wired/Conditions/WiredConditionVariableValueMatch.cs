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

    public override int GetMaxVariableIds() => 2;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.Equals),
            new WiredBoolParamRule(false),
            WiredRules.AnyInt(),
            WiredRules.AnyInt(),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredBoolParamRule(false),
        ];

    private const int PARAM_SAME_HOLDER = 6;

    protected override int GetRequiredIntParamCount() => PARAM_SAME_HOLDER;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var variable = GetVariable(0);

        if (variable is null)
            return false;

        var selection = ctx.GetSelection(this);
        var sameHolder =
            GetIntParamOrDefault(2, false) && GetIntParamOrDefault(PARAM_SAME_HOLDER, false);
        long operand = 0;

        if (!sameHolder && !TryResolveOperand(2, 3, 5, 1, selection, out operand))
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
            true
        );
    }
}
