using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Messages.Outgoing.Groupforums;

namespace Turbo.PacketHandlers.Groupforums;

public class GetUnreadForumsCountMessageHandler : IMessageHandler<GetUnreadForumsCountMessage>
{
    public async ValueTask HandleAsync(
        GetUnreadForumsCountMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // No group has a forum until the forum ship lands (GuildSummarySnapshot.HasForum), so
        // none can hold an unread message. The client polls this at login and every
        // groupforum.poll.period and keeps its last answer, so it is answered rather than left.
        await ctx.SendComposerAsync(
                new UnreadForumsCountMessageComposer { UnreadForumsCount = 0 },
                ct
            )
            .ConfigureAwait(false);
    }
}
