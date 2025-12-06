using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

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
        // :w=4,11 l=32,57 l
        var position = message.WallPosition.Split(' ');

        if (position.Length != 3)
            return;

        var coords = position[0][3..].Split(',');
        var loc = position[1][2..].Split(',');
        var rot = position[2].Equals("l") ? Rotation.South : Rotation.West;

        if (coords.Length != 2 || loc.Length != 2)
            return;

        if (!int.TryParse(coords[0], out var x))
            return;

        if (!int.TryParse(coords[1], out var y))
            return;

        if (!int.TryParse(loc[0], out var wallOffset))
            return;

        if (!double.TryParse(loc[1], out var z))
            return;

        await _roomService
            .MoveWallItemInRoomAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                x,
                y,
                z,
                wallOffset,
                rot,
                ct
            )
            .ConfigureAwait(false);
    }
}
