using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

[RoomObjectLogic("wf_xtra_filter_users")]
public class WiredAddonUserSelectorFilter(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredAddonSelectorFilter(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.USER_SELECTOR_FILTER;

    protected override bool FiltersFurni => false;
}
