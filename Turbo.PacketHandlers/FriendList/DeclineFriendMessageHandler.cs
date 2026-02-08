using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class DeclineFriendMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<DeclineFriendMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        DeclineFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        await messengerGrain
            .DeclineFriendRequestsAsync(message.Friends, message.DeclineAll, ct)
            .ConfigureAwait(false);
    }
}
