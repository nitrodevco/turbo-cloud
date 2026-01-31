using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.IntParams;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_context")]
public class WiredVariableContext(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.Context;

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.Context;
    protected override WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Internal;
    protected override WiredVariableFlags Flags =>
        (
            WiredData.IntParams[0] == 1
                ? WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue
                : WiredVariableFlags.None
        )
        | WiredVariableFlags.CanCreateAndDelete
        | WiredVariableFlags.CanReadCreationTime;

    public override List<IWiredIntParamRule> GetIntParamRules() => [new WiredIntRangeRule(0, 1, 0)];
}
