using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Catalog;
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

        await ctx.PurchaseOfferAsync(
                _grainFactory,
                CatalogType.Normal,
                message.OfferId,
                message.ExtraParam ?? string.Empty,
                message.Quantity,
                ct
            )
            .ConfigureAwait(false);
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

        if (result.BalanceFailure is { } balanceFailure)
        {
            await ctx.SendBalanceFailureAsync(balanceFailure, ct).ConfigureAwait(false);

            return;
        }

        var errorType = result.Error switch
        {
            LtdRaffleEntryErrorType.AlreadyWon => CatalogPurchaseErrorType.LtdPurchasesLimited,
            LtdRaffleEntryErrorType.RaffleProcessing => CatalogPurchaseErrorType.RaffleOngoing,
            LtdRaffleEntryErrorType.SoldOut => CatalogPurchaseErrorType.OfferNotFound,
            LtdRaffleEntryErrorType.InsufficientFunds => CatalogPurchaseErrorType.NotEnoughCredits,
            LtdRaffleEntryErrorType.RequiresHabboClub => CatalogPurchaseErrorType.RequiresHabboClub,
            _ => CatalogPurchaseErrorType.PurchaseFailed,
        };

        await ctx.SendPurchaseErrorAsync(errorType, ct).ConfigureAwait(false);
    }
}
