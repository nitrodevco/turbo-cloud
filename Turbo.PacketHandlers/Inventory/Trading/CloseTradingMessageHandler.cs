using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class CloseTradingMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CloseTradingMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CloseTradingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .CloseTradeAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);
    }
}
