using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_quest_chain")]
public class WiredVariableQuestChain(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.QuestChain;

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.None;
    protected override WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Unknown;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.CanInterceptChanges
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.CanReadLastUpdateTime;
}
