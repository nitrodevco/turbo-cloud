using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Engine;

public class RemoveBotFromFlatMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RemoveBotFromFlatMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RemoveBotFromFlatMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.BotId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .PickupBotAsync(ctx.AsActionContext(), message.BotId, ct)
            .ConfigureAwait(false);
    }
}
