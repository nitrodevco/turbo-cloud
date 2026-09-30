using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// The requests waiting since the last session (<c>HabboFriendList.getFriendRequests</c>). The work
/// is <see cref="IMessengerService"/>'s.
/// </summary>
public class GetFriendRequestsMessageHandler(IMessengerService messengerService)
    : IMessageHandler<GetFriendRequestsMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        GetFriendRequestsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .GetFriendRequestsAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
