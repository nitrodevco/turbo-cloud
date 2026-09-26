using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>Fires when a player uses (double-clicks) one of the picked furni.</summary>
[RoomObjectLogic("wf_trg_stuff_state")]
public class WiredTriggerHabboChangesItemState(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredFurniEventTriggerLogic<RoomItemUsedEvent>(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.USE_STUFF;

    protected override RoomObjectId GetFurniId(RoomItemUsedEvent evt) => evt.ObjectId;
}
