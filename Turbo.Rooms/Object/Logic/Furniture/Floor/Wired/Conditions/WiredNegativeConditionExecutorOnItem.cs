using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

[RoomObjectLogic("wf_cnd_not_trggrer_on")]
public class WiredNegativeConditionExecutorOnItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionExecutorOnItem(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.NOT_TRIGGERER_IS_ON_FURNI;

    public override bool IsNegative() => true;
}
