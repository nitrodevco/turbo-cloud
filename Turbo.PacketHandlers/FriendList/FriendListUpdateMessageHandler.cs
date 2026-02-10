using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;

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

        // The client sends this as a periodic poll.
        // We respond with an empty update (real updates are pushed via grain notifications).
        await ctx.SendComposerAsync(
                new FriendListUpdateMessageComposer { FriendCategories = [], Updates = [] },
                ct
            )
            .ConfigureAwait(false);
    }
}
