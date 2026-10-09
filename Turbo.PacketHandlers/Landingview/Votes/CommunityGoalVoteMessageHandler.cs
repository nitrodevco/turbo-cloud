using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Messages.Incoming.Landingview.Votes;
using Turbo.Primitives.Messages.Outgoing.Landingview.Votes;

namespace Turbo.PacketHandlers.Landingview.Votes;

public class CommunityGoalVoteMessageHandler(ICommunityGoalService goals)
    : IMessageHandler<CommunityGoalVoteMessage>
{
    public async ValueTask HandleAsync(
        CommunityGoalVoteMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new CommunityVoteReceivedEventMessageComposer
                {
                    Acknowledged = await goals
                        .VoteAsync(ctx.PlayerId, message.VoteOption, ct)
                        .ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
