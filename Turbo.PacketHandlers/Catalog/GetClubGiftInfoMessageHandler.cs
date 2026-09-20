using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

public class GetClubGiftInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetClubGiftInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetClubGiftInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var info = await _grainFactory
            .GetCatalogPurchaseGrain(ctx.PlayerId)
            .GetClubGiftInfoAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new ClubGiftInfoEventMessageComposer { Info = info }, ct)
            .ConfigureAwait(false);
    }
}
