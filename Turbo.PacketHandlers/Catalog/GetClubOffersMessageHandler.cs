using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Catalog;

public class GetClubOffersMessageHandler(IGrainFactory grainFactory, ICatalogService catalogService)
    : IMessageHandler<GetClubOffersMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ICatalogService _catalogService = catalogService;

    public async ValueTask HandleAsync(
        GetClubOffersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // What each offer would leave the buyer holding depends on the buyer, so the offers the
        // catalog knows and the subscription the player holds are read side by side and joined
        // here rather than either one knowing about the other.
        var subscription = await _grainFactory
            .GetPlayerSubscriptionGrain(ctx.PlayerId)
            .GetAsync(SubscriptionType.HabboClub, ct)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;

        var offers = ImmutableArray.CreateRange(
            _catalogService.GetClubOffers(),
            offer => offer.ForSubscription(subscription.ExpiresAt, now)
        );

        await ctx.SendComposerAsync(
                new HabboClubOffersMessageComposer
                {
                    Offers = offers,
                    RequestSource = message.RequestSource,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
