using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Catalog;

public class GetHabboClubExtendOfferMessageHandler(
    IGrainFactory grainFactory,
    ICatalogService catalogService
) : IMessageHandler<GetHabboClubExtendOfferMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ICatalogService _catalogService = catalogService;

    public async ValueTask HandleAsync(
        GetHabboClubExtendOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // Receiving this opens the confirmation dialog, so a hotel with nothing to renew with
        // answers nothing at all rather than an empty offer.
        if (_catalogService.GetClubExtendOffer() is not { } offer)
            return;

        var subscription = await _grainFactory
            .GetPlayerSubscriptionGrain(ctx.PlayerId)
            .GetAsync(SubscriptionType.HabboClub, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new HabboClubExtendOfferMessageComposer
                {
                    Offer = new ClubExtendOfferSnapshot(
                        offer.ForSubscription(subscription.ExpiresAt, DateTime.UtcNow)
                    )
                    {
                        OriginalPriceCreditsPerPeriod = offer.OriginalPriceCreditsPerPeriod,
                        OriginalPriceActivityPointsPerPeriod =
                            offer.OriginalPriceActivityPointsPerPeriod,
                        OriginalActivityPointType = offer.OriginalActivityPointType,
                        SubscriptionDaysLeft = subscription.DaysRemaining,
                    },
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
