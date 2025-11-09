using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;

namespace Turbo.PacketHandlers.Quest;

public class GetConcurrentUsersRewardMessageHandler
    : IMessageHandler<GetConcurrentUsersRewardMessage>
{
    public async ValueTask HandleAsync(
        GetConcurrentUsersRewardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
