using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class ConfirmAcceptTradingMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ConfirmAcceptTradingMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ConfirmAcceptTradingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomTradeGrain(ctx.RoomId)
            .ConfirmAsync(ctx.AsActionContext(), true, ct)
            .ConfigureAwait(false);
    }
}
