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
/// Gives credits out of credit chests into the receivers' earnings window, under the category the
/// box is set to (Games or Agency); they reach the purse when claimed there (official client
/// capture 2026-10-24, wired matrix action.45.R3).
/// </summary>
[RoomObjectLogic("wf_act_give_currency")]
public class WiredActionGiveCurrencyFromChest(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredGiveFromChestLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.GIVE_CURRENCY_FROM_CHEST;

    protected override bool AllowsWallFurni => true;

    protected override WiredChestKind Kind => WiredChestKind.Coins;

    protected override IWiredParamRule KindParamRule =>
        new WiredEnumParamRule<WiredEarningsCategory>(WiredEarningsCategory.Games);

    protected override WiredEarningsCategory? EarningsCategory =>
        GetIntParamOrDefault(PARAM_KIND, WiredEarningsCategory.Games);
}
