using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

public class PurchaseFromCatalogMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PurchaseFromCatalogMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PurchaseFromCatalogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        try
        {
            var purchaseGrain = _grainFactory.GetCatalogPurchaseGrain(ctx.PlayerId);
            var offer = await purchaseGrain
                .PurchaseOfferFromCatalogAsync(
                    CatalogType.Normal,
                    message.OfferId,
                    message.ExtraParam ?? string.Empty,
                    message.Quantity,
                    ct
                )
                .ConfigureAwait(false);

            await ctx.SendComposerAsync(new PurchaseOKMessageComposer { Offer = offer }, ct)
                .ConfigureAwait(false);

            var playerGrain = _grainFactory.GetPlayerGrain(ctx.PlayerId);
            var wallet = await playerGrain.GetWalletAsync(ct).ConfigureAwait(false);

            if (offer.CostCredits > 0)
            {
                await ctx.SendComposerAsync(
                        new CreditBalanceEventMessageComposer { Balance = $"{wallet.Credits}.0" },
                        ct
                    )
                    .ConfigureAwait(false);
            }

            if (offer.CostCurrency > 0 && offer.CurrencyType.HasValue)
            {
                await ctx.SendComposerAsync(
                        new ActivityPointsMessageComposer
                        {
                            PointsByCategoryId = wallet.ActivityPointsByCategoryId,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
            }
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

            await ctx.SendComposerAsync(
                    new PurchaseNotAllowedMessageComposer { ErrorType = ex.ErrorType },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
