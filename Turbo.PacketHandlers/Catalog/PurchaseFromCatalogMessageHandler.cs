using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
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
        }
        catch (CatalogPurchaseException ex)
        {
            await ctx.SendComposerAsync(
                    new PurchaseNotAllowedMessageComposer { ErrorType = ex.ErrorType },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
