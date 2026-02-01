using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

[RoomObjectLogic("wf_cnd_not_user_performs_action")]
public class WiredNegativeConditionHabboPerformsAction(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionHabboPerformsAction(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.NOT_PERFORMING_ACTION;

    public override bool IsNegative() => true;
}
