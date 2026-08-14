using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
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

        var snapshot = _catalogService.GetCatalogSnapshot(CatalogType.Normal);

        if (snapshot.OffersById.TryGetValue(message.OfferId, out var offer))
        {
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
        var seriesId = ltdProduct.LtdSeriesId ?? ltdProduct.Id;
        var ltdRaffleGrain = _grainFactory.GetLtdRaffleGrain(seriesId);
        var result = await ltdRaffleGrain.EnterRaffleAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        if (result.Success)
            return;

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

        switch (result.Error)
        {
            case LtdRaffleEntryErrorType.AlreadyWon:
                await ctx.SendComposerAsync(
                        new PurchaseErrorMessageComposer
                        {
                            ErrorCode = (int)CatalogPurchaseErrorType.LtdPurchasesLimited,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;

            case LtdRaffleEntryErrorType.RaffleProcessing:
                await ctx.SendComposerAsync(
                        new PurchaseErrorMessageComposer
                        {
                            ErrorCode = (int)CatalogPurchaseErrorType.RaffleOngoing,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;

            case LtdRaffleEntryErrorType.AlreadyInQueue:
                await ctx.SendComposerAsync(
                        new PurchaseNotAllowedMessageComposer
                        {
                            ErrorType = CatalogPurchaseErrorType.PurchaseFailed,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                break;

            case LtdRaffleEntryErrorType.SoldOut:
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

            case LtdRaffleEntryErrorType.InsufficientFunds:
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
