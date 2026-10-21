using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Habbicons;

/// <summary>A whole collection bought as a set (<c>HabbiconController.buyHabbiconCollection</c>).</summary>
public class BuyHabbiconCollectionMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<BuyHabbiconCollectionMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        BuyHabbiconCollectionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerHabbiconGrain(ctx.PlayerId)
            .BuyCollectionAsync(message.CollectionId, ct)
            .ConfigureAwait(false);
    }
}
