using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Gives credits out of credit chests. The client files them under an earnings category; this
/// server has no earnings to claim, so the credits go straight to the wallet and the category
/// is kept for the editor only.
/// </summary>
[RoomObjectLogic("wf_act_give_currency")]
public class WiredActionGiveCurrencyFromChest(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredGiveFromChestLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.GIVE_CURRENCY_FROM_CHEST;

    protected override WiredChestKind Kind => WiredChestKind.Coins;

    protected override IWiredParamRule KindParamRule =>
        new WiredEnumParamRule<WiredEarningsCategory>(WiredEarningsCategory.Games);
}
