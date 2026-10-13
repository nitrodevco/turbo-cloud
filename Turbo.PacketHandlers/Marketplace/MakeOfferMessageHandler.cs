using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;

namespace Turbo.PacketHandlers.Marketplace;

public class MakeOfferMessageHandler(IMarketplaceService marketplace)
    : IMessageHandler<MakeOfferMessage>
{
    public async ValueTask HandleAsync(
        MakeOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var result = await marketplace
            .MakeOfferAsync(ctx.PlayerId, message.Price, message.ItemRefs, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new MarketplaceMakeOfferResultMessageComposer { Result = result },
                ct
            )
            .ConfigureAwait(false);
    }
}
