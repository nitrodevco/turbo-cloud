using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Extras;

[RoomObjectLogic("wf_xtra_filter_furni")]
public class WiredExtraFilterItems(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_WITH_VARIABLE; // TODO ?????
}
