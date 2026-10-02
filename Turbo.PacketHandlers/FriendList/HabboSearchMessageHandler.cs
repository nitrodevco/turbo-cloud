using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// The friend list's player search (<c>SearchView.searchAvatar</c>). The work is <see
/// cref="IMessengerService"/>'s.
/// </summary>
public class HabboSearchMessageHandler(IMessengerService messengerService)
    : IMessageHandler<HabboSearchMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        HabboSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _messengerService
            .SearchAsync(ctx.PlayerId, message.SearchQuery, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
