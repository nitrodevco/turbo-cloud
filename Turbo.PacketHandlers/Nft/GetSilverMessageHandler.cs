using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Nft;

public class GetSilverMessageHandler(IGrainFactory grainFactory) : IMessageHandler<GetSilverMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetSilverMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var wallet = await _grainFactory
            .GetPlayerGrain(ctx.PlayerId)
            .GetWalletAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new SilverBalanceMessageComposer { SilverBalance = wallet.Silver },
                ct
            )
            .ConfigureAwait(false);
    }
}
