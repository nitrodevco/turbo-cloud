using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;

namespace Turbo.PacketHandlers.FriendList;

public class GetMessengerHistoryMessageHandler : IMessageHandler<GetMessengerHistoryMessage>
{
    public async ValueTask HandleAsync(
        GetMessengerHistoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
