using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

public class PurchaseFromCatalogMessageHandler(
    IGrainFactory grainFactory,
    ICatalogService catalogService
) : IMessageHandler<PurchaseFromCatalogMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ICatalogService _catalogService = catalogService;

    public async ValueTask HandleAsync(
        PurchaseFromCatalogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // 1. Get current catalog snapshot to check for LTDs
        var snapshot = _catalogService.GetCatalogSnapshot(CatalogType.Normal);

        if (snapshot.OffersById.TryGetValue(message.OfferId, out var offer))
        {
            // Detect if any product in this offer is an LTD (UniqueSize > 0)
            var ltdProduct = offer.Products.FirstOrDefault(p => p.UniqueSize > 0);

            if (ltdProduct != null)
            {
                await HandleLtdPurchaseAsync(ctx, ltdProduct, ct).ConfigureAwait(false);
                return;
            }
        }

        try
        {
            var purchaseGrain = _grainFactory.GetCatalogPurchaseGrain(ctx.PlayerId);
            var resultOffer = await purchaseGrain
                .PurchaseOfferFromCatalogAsync(
                    CatalogType.Normal,
                    message.OfferId,
                    message.ExtraParam ?? string.Empty,
                    message.Quantity,
                    ct
                )
                .ConfigureAwait(false);

            await ctx.SendComposerAsync(new PurchaseOKMessageComposer { Offer = resultOffer }, ct)
                .ConfigureAwait(false);
        }
        catch (CatalogPurchaseException ex)
        {
            if (ex.BalanceFailure is not null)
            {
                await ctx.SendComposerAsync(
                        new NotEnoughBalanceMessageComposer
                        {
                            NotEnoughCredits = ex.BalanceFailure.NotEnoughCredits,
                            NotEnoughActivityPoints = ex.BalanceFailure.NotEnoughActivityPoints,
                            ActivityPointType = ex.BalanceFailure.ActivityPointType,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;
            }

            // If error code is < 100, it belongs to the dynamic "PurchaseError" (930) packet
            if ((int)ex.ErrorType < 100)
            {
                await ctx.SendComposerAsync(
                        new PurchaseErrorMessageComposer { ErrorCode = (int)ex.ErrorType },
                        ct
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                // Otherwise use the static "NotAllowed" (1872) packet
                // Map internal RequiresHabboClub (101) to client code 1
                var errorCode =
                    ex.ErrorType == CatalogPurchaseErrorType.RequiresHabboClub
                        ? 1
                        : (int)ex.ErrorType;
                await ctx.SendComposerAsync(
                        new PurchaseNotAllowedMessageComposer
                        {
                            ErrorType = (CatalogPurchaseErrorType)errorCode,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task HandleLtdPurchaseAsync(
        MessageContext ctx,
        CatalogProductSnapshot ltdProduct,
        CancellationToken ct
    )
    {
        // Use the series ID if available (Perfect Design), otherwise fallback to Product ID
        var seriesId = ltdProduct.LtdSeriesId ?? ltdProduct.Id;
        var ltdRaffleGrain = _grainFactory.GetLtdRaffleGrain(seriesId);

        var result = await ltdRaffleGrain.EnterRaffleAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        if (result.Success)
        {
            // If it was an instant buy (FCFS), send standard PurchaseOK
            if (result.BatchId == "instant")
            {
                var snapshot = _catalogService.GetCatalogSnapshot(CatalogType.Normal);
                if (snapshot.OffersById.TryGetValue(ltdProduct.OfferId, out var offer))
                {
                    await ctx.SendComposerAsync(new PurchaseOKMessageComposer { Offer = offer }, ct)
                        .ConfigureAwait(false);
                }
            }
            return;
        }

        // 1. Check for specialized balance alerts first
        if (result.BalanceFailure != null)
        {
            await ctx.SendComposerAsync(
                    new NotEnoughBalanceMessageComposer
                    {
                        NotEnoughCredits = result.BalanceFailure.NotEnoughCredits,
                        NotEnoughActivityPoints = result.BalanceFailure.NotEnoughActivityPoints,
                        ActivityPointType = result.BalanceFailure.ActivityPointType,
                    },
                    ct
                )
                .ConfigureAwait(false);
            return;
        }

        // 2. Map other errors to correct packet types
        switch (result.Error)
        {
            case LtdRaffleEntryError.AlreadyWon:
                // Error 6: "LTD item purchases are limited..." needs PurchaseErrorMessage (930)
                await ctx.SendComposerAsync(
                        new PurchaseErrorMessageComposer
                        {
                            ErrorCode = (int)CatalogPurchaseErrorType.LtdPurchasesLimited,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;

            case LtdRaffleEntryError.RaffleProcessing:
                // Error 12: "Frank is handling..." needs PurchaseErrorMessage (930)
                await ctx.SendComposerAsync(
                        new PurchaseErrorMessageComposer
                        {
                            ErrorCode = (int)CatalogPurchaseErrorType.RaffleOngoing,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;

            case LtdRaffleEntryError.AlreadyInQueue:
                // Silent failure or generic error
                await ctx.SendComposerAsync(
                        new PurchaseNotAllowedMessageComposer
                        {
                            ErrorType = CatalogPurchaseErrorType.PurchaseFailed,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;

            case LtdRaffleEntryError.SoldOut:
                // Map to static PurchaseNotAllowed (1872) using internal ID for OfferNotFound
                await ctx.SendComposerAsync(
                        new PurchaseNotAllowedMessageComposer
                        {
                            ErrorType = (CatalogPurchaseErrorType)
                                (int)CatalogPurchaseErrorType.OfferNotFound,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;

            case LtdRaffleEntryError.InsufficientFunds:
                // Fallback: If 3883 failed or balancefailure null, send standard NotEnoughCredits via 1872 logic
                // Map to client case 102 internally
                await ctx.SendComposerAsync(
                        new PurchaseNotAllowedMessageComposer
                        {
                            ErrorType = CatalogPurchaseErrorType.NotEnoughCredits,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;

            default:
                await ctx.SendComposerAsync(
                        new PurchaseNotAllowedMessageComposer
                        {
                            ErrorType = CatalogPurchaseErrorType.PurchaseFailed,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;
        }
    }
}
