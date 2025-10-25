using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;

namespace Turbo.PacketHandlers.FriendList;

public class DeclineFriendMessageHandler : IMessageHandler<DeclineFriendMessage>
{
    public async ValueTask HandleAsync(
        DeclineFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
