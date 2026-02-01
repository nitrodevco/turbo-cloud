using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

[RoomObjectLogic("wf_cnd_not_hv_avtrs")]
public class WiredNegativeConditionItemHasHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionItemHasHabbo(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.NOT_FURNIS_HAVE_AVATARS;

    public override bool IsNegative() => true;
}
