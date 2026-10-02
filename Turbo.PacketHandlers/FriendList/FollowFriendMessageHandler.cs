using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Follows a friend into their room (<c>FriendsView.onFollowButtonClick</c>) and reports why it
/// could not. The work is <see cref="IMessengerService"/>'s.
/// </summary>
public class FollowFriendMessageHandler(IMessengerService messengerService)
    : IMessageHandler<FollowFriendMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        FollowFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .FollowFriendAsync(ctx.PlayerId, message.PlayerId, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
