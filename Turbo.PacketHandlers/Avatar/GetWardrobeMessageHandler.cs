using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Avatar;

public class GetWardrobeMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetWardrobeMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetWardrobeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var presence = _grainFactory.GetPlayerPresenceGrain(ctx.PlayerId);

        await presence.OnRequestWardrobeAsync(ct).ConfigureAwait(false);
    }
}
