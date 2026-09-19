using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

[RoomObjectLogic("wf_xtra_filter_furni_by_var")]
public class WiredAddonFilterFurniByVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredAddonVariableFilter(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.FURNI_VARIABLE_FILTER;

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.Furni;
}
