using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class AddItemsToTradeMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AddItemsToTradeMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AddItemsToTradeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ItemIds.IsDefaultOrEmpty)
            return;

        await _grainFactory
            .GetRoomTradeGrain(ctx.RoomId)
            .AddItemsAsync(ctx.AsActionContext(), [.. message.ItemIds.Where(x => x > 0)], ct)
            .ConfigureAwait(false);
    }
}
