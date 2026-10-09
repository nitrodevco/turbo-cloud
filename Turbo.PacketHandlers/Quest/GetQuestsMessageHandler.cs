using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Messages.Outgoing.Quest;

namespace Turbo.PacketHandlers.Quest;

public class GetQuestsMessageHandler : IMessageHandler<GetQuestsMessage>
{
    public async ValueTask HandleAsync(
        GetQuestsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // The server has no quests yet, so the list is honestly empty. The client asks only when
        // the player opens the quest window (the toolbar, or "more quests" after one is done),
        // so the answer is the list to open it on.
        await ctx.SendComposerAsync(
                new QuestsMessageComposer { Quests = [], OpenWindow = true },
                ct
            )
            .ConfigureAwait(false);
    }
}
