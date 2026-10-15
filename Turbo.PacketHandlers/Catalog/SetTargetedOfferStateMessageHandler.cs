using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// Where the player left the offer's window (<c>OfferController</c>: 1 when the dialog opens,
/// 4 when it is minimized to the toolbar); the next login opens it the same way.
/// </summary>
public class SetTargetedOfferStateMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetTargetedOfferStateMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetTargetedOfferStateMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerTargetedOfferGrain(ctx.PlayerId)
            .SetTrackingStateAsync(message.TargetedOfferId, message.TrackingState, ct)
            .ConfigureAwait(false);
    }
}
