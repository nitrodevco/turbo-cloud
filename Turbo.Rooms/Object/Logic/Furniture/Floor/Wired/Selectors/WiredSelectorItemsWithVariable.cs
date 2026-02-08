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

[RoomObjectLogic("wf_slc_furni_with_var")]
public class WiredSelectorItemsWithVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_WITH_VARIABLE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.GreaterThan),
            new WiredRangeParamRule(0, 2, 0),
            new WiredParamRule(0),
            new WiredParamRule(0), // set value
            new WiredEnumParamRule<WiredVariableTargetType>(WiredVariableTargetType.Furni),
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

    public override async Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = await ctx.GetWiredSelectionSetAsync(this, ct);
        var allowedDefinitionIds = new List<int>();
        var output = new WiredSelectionSet();

        /* try
        {
            if(_wiredData.VariableIds.Count == 0)
                return output;

            var variableId = WiredVariableId.Parse(_wiredData.VariableIds[0]);
            var variable = _roomGrain.WiredSystem.GetVariableById(variableId);

            if(variable)
            {
                var refValue = 0;

                switch(_wiredData.GetIntParam<int>(1))
                {
                    case 2:
                        {
                            var refVariable = _roomGrain.WiredSystem.GetVariableById(WiredVariableId.Parse(_wiredData.VariableIds[1]));
                        }
                    break;
                    case 1:
                })

                if(_wiredData.GetIntParam<int>(1) == 2)
                {
                    var refVariable = _roomGrain.WiredSystem.GetVariableById(WiredVariableId.Parse(_wiredData.VariableIds[1]));

                    if(refVariable is not null)
                    {
                        var refVarValue = refVariable.TryGetValue()

                        if(refVarValue is int i)
                            refValue = i;
                    }

                    if(refVariable.GetVarSnapshot().TargetType != _wiredData.GetIntParam<WiredVariableTargetType>(4))
                        return output;
                }
            }
            var variableValue = await ctx.GetVariableValueAsync(variableId, ct);

            if (variableValue is null)
                return output;

            var comparisonType = _wiredData.GetIntParam<WiredComparisonType>(0);
            var compareToValue = _wiredData.GetIntParam(2);

            var comparisonResult = variableValue.CompareTo(compareToValue);

            var isMatch =
                comparisonType switch
                {
                    WiredComparisonType.LessThan => comparisonResult < 0,
                    WiredComparisonType.Equals => comparisonResult == 0,
                    WiredComparisonType.GreaterThan => comparisonResult > 0,
                    _ => throw new InvalidOperationException("Invalid comparison type."),
                };

            if (!isMatch)
                return output;
        }

        var variableId = WiredVariableId.Parse(_wiredData.VariableIds[0]);

        if(_wiredData.Vari)

        foreach (var id in input.SelectedFurniIds)
        {
            try
            {
                _roomGrain.WiredSystem.GetVariableById()
                if (!_roomGrain._state.ItemsById.TryGetValue(id, out var item))
                    continue;

                allowedDefinitionIds.Add(item.Definition.Id);
            }
            catch
            {
                continue;
            }
        }

        foreach (var item in _roomGrain._state.ItemsById.Values)
        {
            if (allowedDefinitionIds.Contains(item.Definition.Id))
                output.SelectedFurniIds.Add((int)item.ObjectId);
        } */

        return output;
    }
}
