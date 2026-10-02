using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Accepts friend requests (<c>FriendRequestsView.acceptRequest</c> / <c>acceptAllRequests</c>) and
/// reports the ones refused. The work is <see cref="IMessengerService"/>'s.
/// </summary>
public class AcceptFriendMessageHandler(IMessengerService messengerService)
    : IMessageHandler<AcceptFriendMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        AcceptFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .AcceptFriendRequestsAsync(ctx.PlayerId, message.Friends, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
