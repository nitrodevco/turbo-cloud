using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Incoming.Inventory.Furni;

namespace Turbo.PacketHandlers.Inventory.Furni;

public class RequestFurniInventoryWhenNotInRoomMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RequestFurniInventoryWhenNotInRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RequestFurniInventoryWhenNotInRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var inventory = _grainFactory.GetGrain<IInventoryGrain>(ctx.PlayerId);

        await inventory.SendFurniToPlayerAsync(ct).ConfigureAwait(false);
    }
}
