using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;

namespace Turbo.PacketHandlers.FriendList;

public class AcceptFriendMessageHandler : IMessageHandler<AcceptFriendMessage>
{
    public async ValueTask HandleAsync(
        AcceptFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
