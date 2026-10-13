using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// A Habbicon sent in a conversation from the messenger's picker
/// (<c>MainView.onHabbiconSelected</c>), and the error to show when it is refused. The work is
/// <see cref="IMessengerService"/>'s.
/// </summary>
public class SendHabbiconMessageHandler(IMessengerService messengerService)
    : IMessageHandler<SendHabbiconMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        SendHabbiconMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .SendHabbiconAsync(
                ctx.PlayerId,
                message.ChatId,
                message.HabbiconId,
                message.ConfirmationId,
                ct
            )
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
