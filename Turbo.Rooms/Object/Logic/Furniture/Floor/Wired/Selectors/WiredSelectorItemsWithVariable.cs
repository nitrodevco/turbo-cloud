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
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// Picks the furni holding the chosen variable, optionally only those whose value passes a
/// comparison. Params as the editor saves them (AS3 <c>WithVariable.readIntParamsFromForm</c>):
/// comparison, reference (0 none - "select by value" unticked, 1 a typed value, 2 a variable),
/// the typed value (hi, lo), the reference variable's target. Variables: the subject, then the
/// reference variable.
/// </summary>
[RoomObjectLogic("wf_slc_furni_with_var")]
public class WiredSelectorItemsWithVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    protected const int REFERENCE_NONE = 0;
    protected const int REFERENCE_VALUE = 1;
    protected const int REFERENCE_VARIABLE = 2;

    protected virtual WiredVariableTargetType TargetType => WiredVariableTargetType.Furni;

    public override int WiredCode => (int)WiredSelectorType.FURNI_WITH_VARIABLE;

    public override int GetMaxVariableIds() => 2;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.GreaterThan),
            new WiredRangeParamRule(REFERENCE_NONE, REFERENCE_VARIABLE, REFERENCE_NONE),
            WiredRules.AnyInt(),
            WiredRules.AnyInt(),
            WiredRules.VariableTarget(WiredVariableTargetType.Furni),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [WiredPlayerSourceType.TriggeredUser, WiredPlayerSourceType.SignalUsers],
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var variable = GetVariable(0);

        if (variable is null)
            return Task.FromResult<IWiredSelectionSet>(output);

        var reference = GetIntParamOrDefault(1, REFERENCE_NONE);
        var byValue = reference != REFERENCE_NONE;
        long operand = 0;

        if (reference == REFERENCE_VALUE)
            operand = GetLongParam(2);
        else if (
            reference == REFERENCE_VARIABLE
            && !TryReadVariableOperand(1, 4, ctx.GetSelection(this), out operand)
        )
            return Task.FromResult<IWiredSelectionSet>(output);

        var comparison = GetIntParamOrDefault(0, WiredComparisonType.GreaterThan);

        foreach (var targetId in WiredSystem.GetLiveTargetIds(TargetType))
        {
            var value = ReadVariable(variable, TargetType, targetId);

            if (value is null)
                continue;

            if (byValue && !WiredComparison.Compare(comparison, value.Value, operand))
                continue;

            if (TargetType == WiredVariableTargetType.Furni)
                output.SelectedFurniIds.Add(targetId);
            else
                output.SelectedAvatarIds.Add(targetId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
