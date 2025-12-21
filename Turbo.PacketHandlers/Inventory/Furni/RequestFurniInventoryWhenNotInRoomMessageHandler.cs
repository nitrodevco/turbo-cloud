using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Furni;
using Turbo.Primitives.Orleans;

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
        if (ctx.PlayerId <= 0)
            return;

        var presence = _grainFactory.GetPlayerPresenceGrain(ctx.PlayerId);

        await presence.OpenFurnitureInventoryAsync(ct).ConfigureAwait(false);
    }
}
