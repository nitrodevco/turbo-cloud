using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

public class SelectClubGiftMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SelectClubGiftMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SelectClubGiftMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrEmpty(message.ProductCode))
            return;

        try
        {
            var offer = await _grainFactory
                .GetCatalogPurchaseGrain(ctx.PlayerId)
                .ClaimClubGiftAsync(message.ProductCode, ct)
                .ConfigureAwait(false);

            await ctx.SendComposerAsync(
                    new ClubGiftSelectedEventMessageComposer { Offer = offer },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (CatalogPurchaseException ex)
        {
            // The client took the gift off its own counter as it asked, so a refusal has to put
            // the shelf back the way it really is.
            await ctx.SendPurchaseFailureAsync(ex, ct).ConfigureAwait(false);

            var info = await _grainFactory
                .GetCatalogPurchaseGrain(ctx.PlayerId)
                .GetClubGiftInfoAsync(ct)
                .ConfigureAwait(false);

            await ctx.SendComposerAsync(new ClubGiftInfoEventMessageComposer { Info = info }, ct)
                .ConfigureAwait(false);
        }
    }
}
