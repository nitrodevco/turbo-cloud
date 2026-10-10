using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

/// <summary>The quest tracker asking for its next quest once one is completed.</summary>
public class OpenQuestTrackerMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<OpenQuestTrackerMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        OpenQuestTrackerMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerQuestGrain(ctx.PlayerId)
            .OpenTrackerAsync(ct)
            .ConfigureAwait(false);
    }
}
