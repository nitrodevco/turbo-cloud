using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the picked furni have no furni on them: param 0 is the editor's
/// <c>not_requireall.0</c> "one or more of the selected furni has no furni on it", 1
/// <c>not_requireall.1</c> "all the selected furni have no furni on them". Negating the positive
/// box, so its "all have furni" is quantified the other way round.
/// </summary>
[RoomObjectLogic("wf_cnd_not_furni_on")]
public class WiredNegativeConditionItemHasItems(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionItemHasItems(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.NOT_HAS_STACKED_FURNIS;

    public override bool IsNegative() => true;

    // not (all have furni) = one has none; not (any has furni) = all have none.
    protected override bool RequiresAllFurni() => !RequiresAll();
}
