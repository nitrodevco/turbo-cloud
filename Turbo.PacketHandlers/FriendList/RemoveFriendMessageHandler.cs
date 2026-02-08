using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class RemoveFriendMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RemoveFriendMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RemoveFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        await messengerGrain
            .RemoveFriendsAsync(message.FriendIds, ct)
            .ConfigureAwait(false);
    }
}
