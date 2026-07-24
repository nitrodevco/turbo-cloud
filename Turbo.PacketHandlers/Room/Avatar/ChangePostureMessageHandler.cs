using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Avatar;

public class ChangePostureMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ChangePostureMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ChangePostureMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain
            .SetAvatarPostureAsync(ctx.AsActionContext(), message.PostureType, ct)
            .ConfigureAwait(false);
    }
}
