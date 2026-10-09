using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Messages.Outgoing.Quest;

namespace Turbo.PacketHandlers.Quest;

public class GetCommunityGoalHallOfFameMessageHandler(ICommunityGoalService goals)
    : IMessageHandler<GetCommunityGoalHallOfFameMessage>
{
    public async ValueTask HandleAsync(
        GetCommunityGoalHallOfFameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new CommunityGoalHallOfFameMessageComposer
                {
                    GoalCode = message.GoalCode,
                    Contributors = await goals
                        .GetHallOfFameAsync(message.GoalCode, ct)
                        .ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
