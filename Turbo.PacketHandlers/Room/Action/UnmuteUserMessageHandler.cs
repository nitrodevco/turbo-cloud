using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Action;

public class UnmuteUserMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UnmuteUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UnmuteUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || message.RoomId != ctx.RoomId
            || message.PlayerId <= 0
        )
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .UnmutePlayerAsync(ctx.AsActionContext(), message.PlayerId, ct)
            .ConfigureAwait(false);
    }
}
