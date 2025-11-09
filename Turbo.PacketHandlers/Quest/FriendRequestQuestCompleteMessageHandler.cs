using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;

namespace Turbo.PacketHandlers.Quest;

public class FriendRequestQuestCompleteMessageHandler
    : IMessageHandler<FriendRequestQuestCompleteMessage>
{
    public async ValueTask HandleAsync(
        FriendRequestQuestCompleteMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
