using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class GetLimitedOfferAppearingNextMessageHandler(ICatalogService catalogService)
    : IMessageHandler<GetLimitedOfferAppearingNextMessage>
{
    private readonly ICatalogService _catalogService = catalogService;

    public async ValueTask HandleAsync(
        GetLimitedOfferAppearingNextMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var upcoming = await _catalogService.GetUpcomingLtdAsync(ct).ConfigureAwait(false);

        if (upcoming != null)
        {
            await ctx.SendComposerAsync(
                    new LimitedOfferAppearingNextMessageComposer
                    {
                        AppearsInSeconds = upcoming.SecondsUntil,
                        PageId = upcoming.PageId,
                        OfferId = upcoming.OfferId,
                        ProductClassName = upcoming.ClassName ?? "",
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        else
        {
            // Habbo standard: send -1 if no upcoming LTD
            await ctx.SendComposerAsync(
                    new LimitedOfferAppearingNextMessageComposer
                    {
                        AppearsInSeconds = -1,
                        PageId = -1,
                        OfferId = -1,
                        ProductClassName = "",
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
