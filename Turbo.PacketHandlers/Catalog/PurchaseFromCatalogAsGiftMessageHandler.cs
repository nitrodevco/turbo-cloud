using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// Sent from the gift dialog: an offer bought for another player, wrapped as the buyer chose.
/// The buyer is told it went through as for any purchase. A receiver who does not exist gets the
/// dialog's own "user not found" alert, which lets the buyer correct the name and try again;
/// every other refusal is a purchase error.
/// </summary>
public class PurchaseFromCatalogAsGiftMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PurchaseFromCatalogAsGiftMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PurchaseFromCatalogAsGiftMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        try
        {
            var offer = await _grainFactory
                .GetCatalogPurchaseGrain(ctx.PlayerId)
                .PurchaseOfferAsGiftAsync(
                    new CatalogGiftRequest
                    {
                        OfferId = message.OfferCode,
                        ExtraParam = message.ExtraParam ?? string.Empty,
                        ReceiverName = message.ReceiverName ?? string.Empty,
                        Message = message.Message ?? string.Empty,
                        SpriteId = message.BoxStuffTypeId,
                        BoxType = message.BoxTypeId,
                        RibbonType = message.RibbonTypeId,
                        ShowPurchaserName = message.ShowPurchaserName,
                    },
                    ct
                )
                .ConfigureAwait(false);

            await ctx.SendComposerAsync(new PurchaseOKMessageComposer { Offer = offer }, ct)
                .ConfigureAwait(false);
        }
        catch (CatalogPurchaseException ex)
            when (ex.ErrorType == CatalogPurchaseErrorType.ReceiverNotFound)
        {
            await ctx.SendComposerAsync(new GiftReceiverNotFoundEventMessageComposer(), ct)
                .ConfigureAwait(false);
        }
        catch (CatalogPurchaseException ex)
        {
            await ctx.SendPurchaseFailureAsync(ex, ct).ConfigureAwait(false);
        }
    }
}
