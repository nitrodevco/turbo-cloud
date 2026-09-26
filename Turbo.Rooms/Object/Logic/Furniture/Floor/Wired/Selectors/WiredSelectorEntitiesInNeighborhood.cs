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

    protected override void CollectTile(int tileId, WiredSelectionSet output)
    {
        foreach (var avatar in _roomGrain.AvatarModule.GetAvatarsOnTile(tileId))
        {
            output.SelectedAvatarIds.Add(avatar.ObjectId);
        }
    }
}
