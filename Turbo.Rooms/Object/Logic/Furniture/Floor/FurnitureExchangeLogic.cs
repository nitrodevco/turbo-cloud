using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Credit furni (gold bars, sacks). It has no states to cycle: double-clicking opens the client's
/// redeem dialog, and redeeming credits the owner's wallet with the value in the definition name
/// and destroys the item.
/// </summary>
[RoomObjectLogic("exchange")]
public class FurnitureExchangeLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not RedeemCreditsInteraction)
            return false;

        if (!IsItemOwner(ctx))
            return Reject(ctx, interaction, "not the owner");

        if (!CreditFurniValue.TryParse(_ctx.Definition.Name, out var credits))
            return Reject(ctx, interaction, "definition name carries no credit value");

        var credited = await _roomGrain
            ._grainFactory.GetPlayerWalletGrain(ctx.PlayerId)
            .CreditAsync(new CurrencyKind { CurrencyType = CurrencyType.Credits }, credits, ct);

        if (!credited)
            return Reject(ctx, interaction, "wallet refused the credit");

        // The wallet has the value now; the furni must not survive to be redeemed twice.
        return await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);
    }
}
