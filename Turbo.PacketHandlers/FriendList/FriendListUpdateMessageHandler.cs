using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// The client's friend list poll (<c>HabboFriendList.sendFriendListUpdate</c>), answered with any
/// changes not pushed yet. The work is <see cref="IMessengerService"/>'s.
/// </summary>
public class FriendListUpdateMessageHandler(IMessengerService messengerService)
    : IMessageHandler<FriendListUpdateMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        FriendListUpdateMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .GetFriendListUpdateAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
