using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Habbicons;

/// <summary>A completed collection's reward taken (<c>HabbiconController.claimHabbicon</c>).</summary>
public class ClaimHabbiconMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ClaimHabbiconMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ClaimHabbiconMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerHabbiconGrain(ctx.PlayerId)
            .ClaimAsync(message.HabbiconId, ct)
            .ConfigureAwait(false);
    }
}
