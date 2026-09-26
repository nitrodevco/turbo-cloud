using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

[RoomObjectLogic("wf_trg_walks_off_furni")]
public class WiredTriggerWalksOffItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
)
    : FurnitureWiredFurniEventTriggerLogic<AvatarWalkOffFurniEvent>(
        grainFactory,
        stuffDataFactory,
        ctx
    )
{
    public override int WiredCode => (int)WiredTriggerType.AVATAR_WALKS_OFF_FURNI;

    protected override RoomObjectId GetFurniId(AvatarWalkOffFurniEvent evt) => evt.FurniId;
}
