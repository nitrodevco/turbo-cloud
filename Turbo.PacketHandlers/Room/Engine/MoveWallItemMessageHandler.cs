using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Engine;

public class MoveWallItemMessageHandler(IRoomService roomService)
    : IMessageHandler<MoveWallItemMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        MoveWallItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        if (!WallPosition.TryParse(message.WallPosition, out var wall))
            return;

        await _roomService
            .MoveWallItemInRoomAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                wall.X,
                wall.Y,
                wall.Z,
                wall.WallOffset,
                wall.Rotation,
                ct
            )
            .ConfigureAwait(false);
    }
}
