using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

[RoomObjectLogic("wf_xtra_filter_furni")]
public class WiredAddonFurniSelectorFilter(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredAddonSelectorFilter(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.FURNI_SELECTOR_FILTER;

    protected override bool FiltersFurni => true;
}
