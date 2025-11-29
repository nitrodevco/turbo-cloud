using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.PacketHandlers.Room.Engine;

public class MoveAvatarMessageHandler(IRoomService roomService) : IMessageHandler<MoveAvatarMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        MoveAvatarMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var objectId = RoomObjectId.From(1);

        await _roomService
            .WalkAvatarToAsync(
                ctx.AsActionContext(),
                objectId,
                message.TargetX,
                message.TargetY,
                ct
            )
            .ConfigureAwait(false);
    }
}
