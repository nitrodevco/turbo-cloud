using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// A conversation's scroll-back (<c>MainView.requestHistory</c>): the page of stored messages
/// before the oldest the client holds. The work is <see cref="IMessengerService"/>'s.
/// </summary>
public class GetMessengerHistoryMessageHandler(IMessengerService messengerService)
    : IMessageHandler<GetMessengerHistoryMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        GetMessengerHistoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .GetMessageHistoryAsync(ctx.PlayerId, message.ChatId, message.Message, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
