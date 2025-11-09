using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Landingview.Votes;

namespace Turbo.PacketHandlers.Landingview.Votes;

public class CommunityGoalVoteMessageHandler : IMessageHandler<CommunityGoalVoteMessage>
{
    public async ValueTask HandleAsync(
        CommunityGoalVoteMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
