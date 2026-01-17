using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
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
    public override int WiredCode => (int)WiredVariableType.FURNI_VARIABLE;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntRangeRule(0, 1, 0),
            new WiredIntEnumRule<WiredAvailabilityType>(WiredAvailabilityType.Temporary),
        ];

    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = _variableId,
            VariableName = WiredData.StringParam,
            StorageData = StorageData,
            AvailabilityType = (WiredAvailabilityType)WiredData.IntParams[1],
            TargetType = WiredVariableTargetType.Furni,
            Flags =
                (
                    WiredData.IntParams[0] == 1
                        ? WiredVariableFlags.HasValue
                        : WiredVariableFlags.None
                ) | WiredVariableFlags.CanWriteValue,
            TextConnectors = [],
        };
}
