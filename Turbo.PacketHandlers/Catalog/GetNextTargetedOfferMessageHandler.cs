using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// The client's one question at login (<c>OfferController.productDataReady</c>): the player's
/// running targeted offer, or TargetedOfferNotFound, as Habbo answers when there is none.
/// </summary>
public class GetNextTargetedOfferMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetNextTargetedOfferMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetNextTargetedOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerTargetedOfferGrain(ctx.PlayerId)
            .SendNextOfferAsync(ct)
            .ConfigureAwait(false);
    }
}
