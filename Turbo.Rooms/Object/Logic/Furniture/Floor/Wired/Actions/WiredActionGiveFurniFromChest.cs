using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Gives furni out of furni chests, in the order the box says.</summary>
[RoomObjectLogic("wf_act_give_furni")]
public class WiredActionGiveFurniFromChest(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredGiveFromChestLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.GIVE_FURNI_FROM_CHEST;

    protected override WiredChestKind Kind => WiredChestKind.Furni;

    protected override IWiredParamRule KindParamRule =>
        new WiredEnumParamRule<WiredChestIterationMode>(WiredChestIterationMode.Random);

    protected override WiredChestIterationMode Order =>
        GetIntParamOrDefault(PARAM_KIND, WiredChestIterationMode.Random);
}
