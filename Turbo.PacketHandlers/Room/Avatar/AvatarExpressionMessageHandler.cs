using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Room.Avatar;

public class AvatarExpressionMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AvatarExpressionMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AvatarExpressionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ExpressionId < 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain
            .SetAvatarExpressionAsync(
                ctx.AsActionContext(),
                (AvatarExpressionType)message.ExpressionId,
                ct
            )
            .ConfigureAwait(false);
    }
}
