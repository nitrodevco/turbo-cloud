using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// GetTargetedOffer carries nothing and no view of WIN63-202610131318 sends it; it is answered as
/// GetNextTargetedOffer is (inferred: the only offer a player has is the running one).
/// </summary>
public class GetTargetedOfferMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetTargetedOfferMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetTargetedOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerTargetedOfferGrain(ctx.PlayerId)
            .SendNextOfferAsync(ct)
            .ConfigureAwait(false);
    }
}
