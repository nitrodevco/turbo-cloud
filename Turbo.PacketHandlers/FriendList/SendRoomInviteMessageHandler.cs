using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// A room invitation to the selected friends (<c>RoomInviteView.sendMsg</c>), and the ones it could
/// not reach. The work is <see cref="IMessengerService"/>'s.
/// </summary>
public class SendRoomInviteMessageHandler(IMessengerService messengerService)
    : IMessageHandler<SendRoomInviteMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        SendRoomInviteMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .SendRoomInviteAsync(ctx.PlayerId, message.FriendIds, message.Message, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
