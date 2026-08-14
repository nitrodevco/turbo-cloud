using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;

namespace Turbo.PacketHandlers.FriendList;

public class FriendListUpdateMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<FriendListUpdateMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        FriendListUpdateMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new FriendListUpdateMessageComposer { Categories = [], Updates = [] },
                ct
            )
            .ConfigureAwait(false);
    }
}
