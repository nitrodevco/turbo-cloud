using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;

namespace Turbo.PacketHandlers.Quest;

public class GetCommunityGoalProgressMessageHandler
    : IMessageHandler<GetCommunityGoalProgressMessage>
{
    public async ValueTask HandleAsync(
        GetCommunityGoalProgressMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
