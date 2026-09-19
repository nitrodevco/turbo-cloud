using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class ConfirmDeclineTradingMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ConfirmDeclineTradingMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ConfirmDeclineTradingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ConfirmTradeAsync(ctx.AsActionContext(), false, ct)
            .ConfigureAwait(false);
    }
}
