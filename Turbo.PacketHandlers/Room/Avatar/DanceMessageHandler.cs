using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Room.Avatar;

public class DanceMessageHandler(IGrainFactory grainFactory) : IMessageHandler<DanceMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        DanceMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        if (
            !await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .SetAvatarDanceAsync(ctx.AsActionContext(), (AvatarDanceType)message.DanceId, ct)
                .ConfigureAwait(false)
        )
        {
            return;
        }
    }
}
