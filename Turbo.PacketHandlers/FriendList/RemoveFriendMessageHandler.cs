using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Ends friendships (<c>FriendRemoveView</c>). The work is <see cref="IMessengerService"/>'s.
/// </summary>
public class RemoveFriendMessageHandler(IMessengerService messengerService)
    : IMessageHandler<RemoveFriendMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        RemoveFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _messengerService
            .RemoveFriendsAsync(ctx.PlayerId, message.FriendIds, ct)
            .ConfigureAwait(false);
    }
}
