using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

public class HasClaimedProductMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<HasClaimedProductMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        HasClaimedProductMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var hasClaimed = await _grainFactory
            .GetPlayerProductClaimGrain(ctx.PlayerId)
            .HasClaimedAsync(message.ClaimId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new HasClaimedProductResponseMessageComposer
                {
                    ClaimId = message.ClaimId,
                    HasClaimed = hasClaimed,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
