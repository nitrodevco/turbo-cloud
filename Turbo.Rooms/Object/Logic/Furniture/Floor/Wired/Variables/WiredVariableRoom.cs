using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.IntParams;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_room")]
public class WiredVariableRoom(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.Global;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntEnumRule<WiredAvailabilityType>(
                WiredAvailabilityType.RoomActive,
                WiredAvailabilityType.RoomActive,
                WiredAvailabilityType.Persistent,
                WiredAvailabilityType.Shared
            ),
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots()
    {
        var snapshot = GetVarSnapshot();

        return
        [
            new WiredVariableInfoAndValueSnapshot()
            {
                ContextType = WiredContextType.VariableInfoAndValue,
                Variable = snapshot,
                Value = TryGetValue(
                    new WiredVariableKey(snapshot.VariableId, snapshot.TargetType, 0),
                    out var value
                )
                    ? value
                    : WiredVariableValue.Default,
            },
        ];
    }

    protected override WiredVariableSnapshot BuildVarSnapshot()
    {
        var variableName = WiredData.StringParam;
        var variableType = WiredVariableType.Created;
        var availabilityType = (WiredAvailabilityType)WiredData.IntParams[0];
        var targetType = WiredVariableTargetType.Global;
        var flags =
            WiredVariableFlags.HasValue
            | WiredVariableFlags.CanWriteValue
            | WiredVariableFlags.CanInterceptChanges
            | WiredVariableFlags.AlwaysAvailable
            | WiredVariableFlags.CanReadLastUpdateTime;
        var textConnectors = GetTextConnectors();
        var variableHash = WiredVariableHashBuilder.HashValues(
            variableName,
            availabilityType,
            targetType,
            flags,
            textConnectors
        );

        return new()
        {
            VariableId = _variableId,
            VariableName = variableName,
            VariableType = variableType,
            VariableHash = variableHash,
            AvailabilityType = availabilityType,
            TargetType = targetType,
            Flags = flags,
            TextConnectors = textConnectors,
        };
    }
}
