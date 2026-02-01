using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.IntParams;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_user")]
public class WiredVariableUser(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.User;

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.User;
    protected override WiredAvailabilityType AvailabilityType =>
        (WiredAvailabilityType)WiredData.IntParams[0];
    protected override WiredVariableFlags Flags =>
        (
            WiredData.IntParams[1] == 1
                ? WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue
                : WiredVariableFlags.None
        )
        | WiredVariableFlags.CanCreateAndDelete
        | WiredVariableFlags.CanInterceptChanges
        | WiredVariableFlags.CanReadCreationTime;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntEnumRule<WiredAvailabilityType>(
                WiredAvailabilityType.UserActive,
                WiredAvailabilityType.UserActive,
                WiredAvailabilityType.Persistent,
                WiredAvailabilityType.Shared
            ),
            new WiredIntEnumRule<WiredBooleanType>(WiredBooleanType.False),
        ];
}
