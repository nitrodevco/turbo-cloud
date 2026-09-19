using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

[RoomObjectLogic("wf_cnd_neg_has_var")]
public class WiredNegativeConditionHasVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionHasVariable(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.NOT_HAS_VARIABLE;

    public override bool IsNegative() => true;
}
