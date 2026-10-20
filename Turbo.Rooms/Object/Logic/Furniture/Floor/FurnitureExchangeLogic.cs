using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Credit furni (gold bars, sacks). It has no states to cycle: double-clicking opens the client's
/// redeem dialog, and redeeming credits the owner's wallet with the value in the definition name
/// and destroys the item. A furni lent by the Builders Club or placed by wired is not the
/// player's to cash in, though the client shows them the dialog: it opens it for any furni whose
/// owner id is theirs (WIN63-202610131318 <c>FurnitureCreditWidgetHandler.as:60</c>), and a
/// temporary furni carries the room owner's (official client capture 2026-10-14).
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

        if (!IsOwnedOutrightBy(ctx))
            return Reject(ctx, interaction, "a borrowed or temporary furni is worth nothing");

        if (!CreditFurniValue.TryParse(_ctx.Definition.Name, out var credits))
            return Reject(ctx, interaction, "definition name carries no credit value");

        var cause = MovementCause.New(
            MovementCauses.USE_CREDIT_FURNI,
            ctx.PlayerId.Value,
            _ctx.ObjectId.Value
        );

        // The furni goes first, written before anything is paid: credited first, a delete that
        // failed or a crash in between left the furni to be redeemed again.
        if (!await ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, cause, ct))
            return false;

        // Under the furni's own reference, so the value is paid once whatever retries it.
        var credited = await _roomGrain
            ._grainFactory.GetPlayerWalletGrain(ctx.PlayerId)
            .CreditAsync(
                CurrencyKind.Credits,
                credits,
                CreditReference(_ctx.ObjectId),
                cause.StartingAt(1),
                ct
            );

        if (credited != WalletCreditResult.Rejected)
            return true;

        // The wallet took nothing (the balance would overflow): the furni goes back to its owner.
        await ActionModule.GiveBackUsedItemAsync(_ctx.RoomObject, ct);

        return Reject(ctx, interaction, "wallet refused the credit");
    }

    /// <summary>The wallet reference a redeemed furni is paid under: one payment per furni.</summary>
    public static string CreditReference(RoomObjectId itemId) => $"creditfurni:{itemId.Value}";
}
