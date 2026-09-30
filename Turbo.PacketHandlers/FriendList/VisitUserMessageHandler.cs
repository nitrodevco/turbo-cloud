using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Goes to the room a player, found by name, is in. The client then enters it the normal way, so
/// door checks still apply. The work is <see cref="IMessengerService"/>'s.
/// </summary>
public class VisitUserMessageHandler(IMessengerService messengerService)
    : IMessageHandler<VisitUserMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        VisitUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _messengerService
            .VisitUserAsync(ctx.PlayerId, message.PlayerName, ct)
            .ConfigureAwait(false);
    }
}
