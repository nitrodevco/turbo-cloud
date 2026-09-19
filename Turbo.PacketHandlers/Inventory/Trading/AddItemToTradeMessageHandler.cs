using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class AddItemToTradeMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AddItemToTradeMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AddItemToTradeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ItemId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .AddTradeItemsAsync(ctx.AsActionContext(), [message.ItemId], ct)
            .ConfigureAwait(false);
    }
}
