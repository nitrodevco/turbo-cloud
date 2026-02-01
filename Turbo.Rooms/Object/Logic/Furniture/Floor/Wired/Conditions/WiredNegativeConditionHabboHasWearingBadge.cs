using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

[RoomObjectLogic("wf_cnd_not_habbo_owns_badge")]
public class WiredNegativeConditionHabboHasWearingBadge(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionHabboHasWearingBadge(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.ACTOR_IS_WEARING_BADGE;

    public override bool IsNegative() => true;
}
