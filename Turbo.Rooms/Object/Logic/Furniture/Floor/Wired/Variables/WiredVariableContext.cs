using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_context")]
public class WiredVariableContext(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.Context;

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.Context;
    protected override WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Internal;
    protected override WiredVariableFlags Flags =>
        (
            _wiredData.GetIntParam<bool>(0)
                ? WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue
                : WiredVariableFlags.None
        )
        | WiredVariableFlags.CanCreateAndDelete
        | WiredVariableFlags.CanReadCreationTime;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];
}
