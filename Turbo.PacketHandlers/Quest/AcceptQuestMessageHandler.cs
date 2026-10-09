using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

/// <summary>Accepting a quest in a room (QuestDetails, QuestsList).</summary>
public class AcceptQuestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AcceptQuestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AcceptQuestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerQuestGrain(ctx.PlayerId)
            .AcceptAsync(message.QuestId, ct)
            .ConfigureAwait(false);
    }
}
