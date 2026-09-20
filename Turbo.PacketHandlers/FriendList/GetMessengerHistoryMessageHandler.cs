using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Consumes the request until a message history is kept. The client's second field is a cursor,
/// not text: it is the id of the oldest message it already holds (empty on the first ask), and it
/// puts what comes back in front of that. <c>PlayerMessengerGrain</c> buffers only the running
/// session, so there is no page before the cursor to answer with.
/// </summary>
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
