using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Action;

public class MuteAllInRoomMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<MuteAllInRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        MuteAllInRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ToggleRoomMuteAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);
    }
}
