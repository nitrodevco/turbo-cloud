using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

public class PurchaseRewardTrackPremiumMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PurchaseRewardTrackPremiumMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PurchaseRewardTrackPremiumMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerRewardTrackGrain(ctx.PlayerId)
            .PurchasePremiumAsync(message.TrackId, ct)
            .ConfigureAwait(false);
    }
}
