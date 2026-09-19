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
/// Params: target, comparison, operand mode, operand (hi, lo), operand target.
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
            new WiredParamRule((int)WiredVariableTargetType.User),
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.Equals),
            new WiredBoolParamRule(false),
            new WiredParamRule(0),
            new WiredParamRule(0),
            new WiredParamRule((int)WiredVariableTargetType.User),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        [
            new WiredVariableAllInRoomSnapshot()
            {
                ContextType = WiredContextType.AllVariablesInRoom,
                AllVariablesHash = _roomGrain._state.AllVariablesHash,
            },
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var variable = GetVariable(0);

        if (variable is null)
            return false;

        var selection = ctx.GetSelection(this);

        if (!TryResolveOperand(2, 3, 5, 1, selection, out var operand))
            return false;

        var targetType = (WiredVariableTargetType)GetIntParamOrDefault(
            0,
            (int)variable.GetVarSnapshot().TargetType
        );
        var comparison = GetIntParamOrDefault(1, WiredComparisonType.Equals);
        var targets = GetTargetIds(targetType, selection).ToList();

        return Quantify(
            targets.Select(id =>
                ReadVariable(variable, targetType, id) is { } value
                && WiredComparison.Compare(comparison, value, operand)
            ),
            true
        );
    }
}
