using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Engine;

public class PlaceBotMessageHandler(IGrainFactory grainFactory) : IMessageHandler<PlaceBotMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PlaceBotMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.BotId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .PlaceBotAsync(ctx.AsActionContext(), message.BotId, message.X, message.Y, ct)
            .ConfigureAwait(false);
    }
}
