using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// Answered with <c>IsOfferGiftable</c>, the message the client reads for it (offer id, giftable),
/// carrying the same <c>can_gift</c> the offer shows on its catalogue page. Like
/// <c>GetProductOffer</c>, an offer the catalogue does not hold is answered nothing. The Flash
/// client registers the answer but its only sender (<c>HabboCatalog.checkGiftable</c>) is never
/// called, so no official reply was ever captured; a gift purchase is checked again when made.
/// </summary>
public class GetIsOfferGiftableMessageHandler(ICatalogService catalogService)
    : IMessageHandler<GetIsOfferGiftableMessage>
{
    private readonly ICatalogService _catalogService = catalogService;

    public async ValueTask HandleAsync(
        GetIsOfferGiftableMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var snapshot = await _catalogService
            .GetCatalogSnapshotForPlayerAsync(CatalogType.Normal, ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (!snapshot.OffersById.TryGetValue(message.OfferId, out var offer))
            return;

        await ctx.SendComposerAsync(
                new IsOfferGiftableMessageComposer
                {
                    OfferId = offer.Id,
                    IsGiftable = offer.CanGift,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
