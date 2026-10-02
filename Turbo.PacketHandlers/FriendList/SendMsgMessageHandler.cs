using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// A console message to a friend or a group chat (<c>MainView.onInput</c>), and the error to show
/// when it is refused. The work is <see cref="IMessengerService"/>'s.
/// </summary>
public class SendMsgMessageHandler(IMessengerService messengerService)
    : IMessageHandler<SendMsgMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        SendMsgMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .SendMessageAsync(
                ctx.PlayerId,
                message.ChatId,
                message.Message,
                message.ConfirmationId,
                ct
            )
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
