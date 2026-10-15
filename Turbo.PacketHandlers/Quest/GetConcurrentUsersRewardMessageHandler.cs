using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Messages.Incoming.Quest;

namespace Turbo.PacketHandlers.Quest;

/// <summary>
/// The concurrent users element's click. The client asks for the progress again straight after,
/// which shows the reward taken.
/// </summary>
public class GetConcurrentUsersRewardMessageHandler(IConcurrentUsersGoalService goal)
    : IMessageHandler<GetConcurrentUsersRewardMessage>
{
    public async ValueTask HandleAsync(
        GetConcurrentUsersRewardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await goal.ClaimRewardAsync(ctx.PlayerId, ct).ConfigureAwait(false);
    }
}
