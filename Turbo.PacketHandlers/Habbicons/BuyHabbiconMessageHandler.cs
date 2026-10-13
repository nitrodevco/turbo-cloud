using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Habbicons;

/// <summary>A Habbicon bought on its own (<c>HabbiconController.buyHabbicon</c>).</summary>
public class BuyHabbiconMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<BuyHabbiconMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        BuyHabbiconMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerHabbiconGrain(ctx.PlayerId)
            .BuyAsync(message.HabbiconId, ct)
            .ConfigureAwait(false);
    }
}
