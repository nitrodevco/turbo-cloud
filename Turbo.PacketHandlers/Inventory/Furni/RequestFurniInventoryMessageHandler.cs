using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Furni;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Furni;

public class RequestFurniInventoryMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RequestFurniInventoryMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RequestFurniInventoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .SendFurnitureInventoryAsync(ct)
            .ConfigureAwait(false);
    }
}
