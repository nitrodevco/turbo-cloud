using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class MessengerInitMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<MessengerInitMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        MessengerInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var presence = _grainFactory.GetPlayerPresenceGrain(ctx.PlayerId);

        await presence.OnInitMessengerAsync(ct).ConfigureAwait(false);
    }
}
