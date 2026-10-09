using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Messages.Outgoing.Quest;

namespace Turbo.PacketHandlers.Quest;

public class GetCommunityGoalProgressMessageHandler(ICommunityGoalService goals)
    : IMessageHandler<GetCommunityGoalProgressMessage>
{
    public async ValueTask HandleAsync(
        GetCommunityGoalProgressMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        // No goal has started: the widget stays empty, as it does before an answer.
        if (
            await goals.GetProgressAsync(ctx.PlayerId, ct).ConfigureAwait(false) is not { } progress
        )
            return;

        await ctx.SendComposerAsync(
                new CommunityGoalProgressMessageComposer { Data = progress },
                ct
            )
            .ConfigureAwait(false);
    }
}
