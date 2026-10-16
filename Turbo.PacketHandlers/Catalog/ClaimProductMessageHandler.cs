using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

public class ClaimProductMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ClaimProductMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ClaimProductMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var result = await _grainFactory
            .GetPlayerProductClaimGrain(ctx.PlayerId)
            .ClaimAsync(message.ClaimId, message.Choice, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ClaimProductResultMessageComposer
                {
                    ClaimId = message.ClaimId,
                    Result = result,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
