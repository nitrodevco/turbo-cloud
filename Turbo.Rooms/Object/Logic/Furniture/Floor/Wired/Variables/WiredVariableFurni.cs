using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.IntParams;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_furni")]
public class WiredVariableFurni(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.Furni;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntRangeRule(0, 1, 0),
            new WiredIntEnumRule<WiredAvailabilityType>(
                WiredAvailabilityType.RoomActive,
                WiredAvailabilityType.RoomActive,
                WiredAvailabilityType.Persistent
            ),
        ];

    protected override WiredVariableSnapshot BuildVarSnapshot()
    {
        var availabilityType = (WiredAvailabilityType)WiredData.IntParams[1];
        var targetType = WiredVariableTargetType.Furni;
        var flags =
            (
                WiredData.IntParams[0] == 1
                    ? WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue
                    : WiredVariableFlags.None
            )
            | WiredVariableFlags.CanCreateAndDelete
            | WiredVariableFlags.CanInterceptChanges
            | WiredVariableFlags.CanReadCreationTime;
        var textConnectors = GetTextConnectors();
        var variableHash = WiredVariableHashBuilder.HashValues(
            WiredData.StringParam,
            availabilityType,
            targetType,
            flags,
            textConnectors
        );

        return new()
        {
            VariableId = _variableId,
            VariableName = WiredData.StringParam,
            VariableType = WiredVariableType.Created,
            VariableHash = variableHash,
            AvailabilityType = availabilityType,
            TargetType = targetType,
            Flags = flags,
            TextConnectors = textConnectors,
        };
    }
}
