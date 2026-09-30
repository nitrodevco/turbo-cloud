using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// The friend bar's "find new friends": to a random room with players in it. The work is <see
/// cref="IMessengerService"/>'s.
/// </summary>
public class FindNewFriendsMessageHandler(IMessengerService messengerService)
    : IMessageHandler<FindNewFriendsMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        FindNewFriendsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _messengerService.FindNewFriendsAsync(ctx.PlayerId, ct).ConfigureAwait(false);
    }
}
