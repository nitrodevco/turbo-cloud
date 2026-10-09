using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

public class ClaimRewardTrackPrizeMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ClaimRewardTrackPrizeMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ClaimRewardTrackPrizeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerRewardTrackGrain(ctx.PlayerId)
            .ClaimPrizeAsync(message.TrackId, message.PrizeId, ct)
            .ConfigureAwait(false);
    }
}
