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
/// comparison. Params: comparison, select-by-value flag, operand mode, operand (hi, lo),
/// operand target. Variables: the subject, then the operand variable.
/// </summary>
[RoomObjectLogic("wf_slc_furni_with_var")]
public class WiredSelectorItemsWithVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    protected virtual WiredVariableTargetType TargetType => WiredVariableTargetType.Furni;

    public override int WiredCode => (int)WiredSelectorType.FURNI_WITH_VARIABLE;

    public override int GetMaxVariableIds() => 2;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.GreaterThan),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredParamRule(0),
            new WiredParamRule(0),
            new WiredParamRule((int)WiredVariableTargetType.Furni),
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
        [
            new WiredVariableAllInRoomSnapshot()
            {
                ContextType = WiredContextType.AllVariablesInRoom,
                AllVariablesHash = _roomGrain._state.AllVariablesHash,
            },
        ];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var variable = GetVariable(0);

        if (variable is null)
            return Task.FromResult<IWiredSelectionSet>(output);

        var byValue = GetIntParamOrDefault(1, false);
        long operand = 0;

        if (byValue && !TryResolveOperand(2, 3, 5, 1, ctx.GetSelection(this), out operand))
            return Task.FromResult<IWiredSelectionSet>(output);

        var comparison = GetIntParamOrDefault(0, WiredComparisonType.GreaterThan);

        foreach (var targetId in EnumerateTargets())
        {
            var value = ReadVariable(variable, TargetType, targetId);

            if (value is null)
                continue;

            if (byValue && !WiredComparison.Compare(comparison, value.Value, operand))
                continue;

            if (TargetType == WiredVariableTargetType.Furni)
                output.SelectedFurniIds.Add(targetId);
            else
                output.SelectedPlayerIds.Add(targetId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }

    protected virtual IEnumerable<int> EnumerateTargets()
    {
        foreach (var item in _roomGrain._state.ItemsById.Values)
            yield return item.ObjectId;
    }
}
