using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Navigator;

public class RateFlatMessageHandler(IGrainFactory grainFactory) : IMessageHandler<RateFlatMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RateFlatMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        // The client only ever votes once, up or down.
        if (message.Points is not (1 or -1))
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .RateRoomAsync(ctx.AsActionContext(), message.Points, ct)
            .ConfigureAwait(false);
    }
}
