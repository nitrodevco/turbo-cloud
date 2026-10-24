using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

[RoomObjectLogic("wf_slc_users_neighborhood")]
public class WiredSelectorEntitiesInNeighborhood(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredNeighborhoodSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_IN_NEIGHBORHOOD;

    public override bool SelectsFurni => false;

    public override bool SelectsUsers => true;

    // Ascending room index, bots included (official client capture 2026-10-24T18-20-40, W4 lab
    // selector.12.R4: "Frank,Hutch.,frank" for indices 0,2,3, 2/2).
    public override WiredUserOrderType UserOrder => WiredUserOrderType.RoomIndex;

    protected override void CollectTile(int tileId, WiredSelectionSet output)
    {
        foreach (var avatar in AvatarModule.GetAvatarsOnTile(tileId))
        {
            output.SelectedAvatarIds.Add(avatar.ObjectId);
        }
    }
}
