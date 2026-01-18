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

[RoomObjectLogic("wf_var_context")]
public class WiredVariableContext(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableType.Context;

    public override List<IWiredIntParamRule> GetIntParamRules() => [new WiredIntRangeRule(0, 1, 0)];

    protected override WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = _variableId,
            VariableName = WiredData.StringParam,
            AvailabilityType = WiredAvailabilityType.None,
            TargetType = WiredVariableTargetType.Context,
            Flags =
                (
                    WiredData.IntParams[0] == 1
                        ? WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue
                        : WiredVariableFlags.None
                )
                | WiredVariableFlags.CanCreateAndDelete
                | WiredVariableFlags.CanReadCreationTime,
            TextConnectors = [],
        };
}
