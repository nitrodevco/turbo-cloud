using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

[RoomObjectLogic("wf_cnd_not_wearing_fx")]
public class WiredNegativeConditionHabboHasEffect(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionHabboHasEffect(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.NOT_ACTOR_IS_WEARING_EFFECT;

    public override bool IsNegative() => true;
}
