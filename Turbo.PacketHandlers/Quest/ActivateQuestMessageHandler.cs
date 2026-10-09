using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

/// <summary>Activating a quest outside a room (QuestDetails): the same as accepting it.</summary>
public class ActivateQuestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ActivateQuestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ActivateQuestMessage message,
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
