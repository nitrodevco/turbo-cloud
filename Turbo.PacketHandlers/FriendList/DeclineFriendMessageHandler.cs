using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Declines friend requests, or all of them. The client has already marked them declined. The work
/// is <see cref="IMessengerService"/>'s.
/// </summary>
public class DeclineFriendMessageHandler(IMessengerService messengerService)
    : IMessageHandler<DeclineFriendMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        DeclineFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _messengerService
            .DeclineFriendRequestsAsync(ctx.PlayerId, message.Friends, message.DeclineAll, ct)
            .ConfigureAwait(false);
    }
}
