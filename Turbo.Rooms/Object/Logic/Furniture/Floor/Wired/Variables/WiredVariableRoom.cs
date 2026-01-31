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

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.Global;
    protected override WiredAvailabilityType AvailabilityType =>
        (WiredAvailabilityType)WiredData.IntParams[0];
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.CanInterceptChanges
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.CanReadLastUpdateTime;

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
}
