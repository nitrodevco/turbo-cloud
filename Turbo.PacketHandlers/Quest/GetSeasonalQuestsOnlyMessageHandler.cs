using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

/// <summary>The seasonal calendar asks for its quests when it opens and after one is completed.</summary>
public class GetSeasonalQuestsOnlyMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetSeasonalQuestsOnlyMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetSeasonalQuestsOnlyMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerQuestGrain(ctx.PlayerId)
            .SendSeasonalQuestsAsync(ct)
            .ConfigureAwait(false);
    }
}
