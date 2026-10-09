using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

/// <summary>The tracker's cancel: drops whichever quest the player is doing.</summary>
public class CancelQuestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CancelQuestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CancelQuestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerQuestGrain(ctx.PlayerId)
            .RejectAsync(0, ct)
            .ConfigureAwait(false);
    }
}
