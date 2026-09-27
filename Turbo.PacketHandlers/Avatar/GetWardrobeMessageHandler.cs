using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Avatar;

public class GetWardrobeMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetWardrobeMessage>
{
    // The only state the client's wardrobe reply knows.
    private const int WARDROBE_STATE_LOADED = 1;

    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetWardrobeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var outfits = await _grainFactory
            .GetPlayerWardrobeGrain(ctx.PlayerId)
            .GetOutfitsAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new WardrobeMessageComposer { State = WARDROBE_STATE_LOADED, Outfits = outfits },
                ct
            )
            .ConfigureAwait(false);
    }
}
