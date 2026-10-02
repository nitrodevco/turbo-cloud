using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// The messenger's first load: limits, categories, friends and the messages that waited. The work
/// is <see cref="IMessengerService"/>'s.
/// </summary>
public class MessengerInitMessageHandler(IMessengerService messengerService)
    : IMessageHandler<MessengerInitMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        MessengerInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _messengerService.SendInitAsync(ctx.PlayerId, ct).ConfigureAwait(false);
    }
}
