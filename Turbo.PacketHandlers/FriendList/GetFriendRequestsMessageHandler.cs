using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class GetFriendRequestsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetFriendRequestsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetFriendRequestsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var requests = await messengerGrain
            .GetFriendRequestsAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new FriendRequestsMessageComposer { Requests = requests },
                ct
            )
            .ConfigureAwait(false);
    }
}
