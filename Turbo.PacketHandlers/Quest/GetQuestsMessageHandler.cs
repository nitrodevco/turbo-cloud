using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

/// <summary>The quest window asks only when the player opens it, so the list opens it.</summary>
public class GetQuestsMessageHandler(IGrainFactory grainFactory) : IMessageHandler<GetQuestsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetQuestsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerQuestGrain(ctx.PlayerId)
            .SendQuestsAsync(true, ct)
            .ConfigureAwait(false);
    }
}
