using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Action;

public class MuteUserMessageHandler(IGrainFactory grainFactory) : IMessageHandler<MuteUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        MuteUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || message.RoomId != ctx.RoomId
            || message.PlayerId <= 0
            || message.DurationInMinutes <= 0
        )
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .MutePlayerAsync(ctx.AsActionContext(), message.PlayerId, message.DurationInMinutes, ct)
            .ConfigureAwait(false);
    }
}
