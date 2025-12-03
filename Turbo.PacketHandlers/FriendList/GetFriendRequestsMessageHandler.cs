using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;

namespace Turbo.PacketHandlers.FriendList;

public class GetFriendRequestsMessageHandler : IMessageHandler<GetFriendRequestsMessage>
{
    public async ValueTask HandleAsync(
        GetFriendRequestsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(new FriendRequestsMessageComposer { Requests = [] }, ct)
            .ConfigureAwait(false);
    }
}
