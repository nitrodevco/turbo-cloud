using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Habbicons;

/// <summary>The Habbicon window's shop (<c>HabbiconController.getShopData</c>).</summary>
public class GetHabbiconShopDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetHabbiconShopDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetHabbiconShopDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerHabbiconGrain(ctx.PlayerId)
            .SendShopDataAsync(ct)
            .ConfigureAwait(false);
    }
}
