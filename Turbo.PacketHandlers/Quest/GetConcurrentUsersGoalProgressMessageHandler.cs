using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Messages.Outgoing.Quest;

namespace Turbo.PacketHandlers.Quest;

/// <summary>The reception's concurrent users element, on refresh and every five seconds after.</summary>
public class GetConcurrentUsersGoalProgressMessageHandler(IConcurrentUsersGoalService goal)
    : IMessageHandler<GetConcurrentUsersGoalProgressMessage>
{
    public async ValueTask HandleAsync(
        GetConcurrentUsersGoalProgressMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new ConcurrentUsersGoalProgressMessageComposer
                {
                    Data = await goal.GetProgressAsync(ctx.PlayerId, ct).ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
