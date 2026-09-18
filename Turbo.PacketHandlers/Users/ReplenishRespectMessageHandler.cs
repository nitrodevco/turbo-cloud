using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Refills the daily respects when the player has a replenish left; the client updates its own counters.
/// </summary>
public class ReplenishRespectMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ReplenishRespectMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ReplenishRespectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerGrain(ctx.PlayerId)
            .ReplenishRespectAsync(ct)
            .ConfigureAwait(false);
    }
}
