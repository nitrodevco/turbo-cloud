using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Room.Engine;

public class PlaceObjectMessageHandler(IRoomService roomService)
    : IMessageHandler<PlaceObjectMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        PlaceObjectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var separator = message.Data.IndexOf(' ');

        if (separator <= 0 || !int.TryParse(message.Data[..separator], out var id))
            return;

        var itemId = Math.Abs(id);
        var location = message.Data[(separator + 1)..];

        if (WallPosition.TryParse(location, out var wall))
        {
            await _roomService
                .PlaceWallItemInRoomAsync(
                    ctx.AsActionContext(),
                    itemId,
                    wall.X,
                    wall.Y,
                    wall.Z,
                    wall.WallOffset,
                    wall.Rotation,
                    ct
                )
                .ConfigureAwait(false);
        }
        else
        {
            var position = location.Split(' ');

            if (position.Length != 3)
                return;

            await _roomService
                .PlaceFloorItemInRoomAsync(
                    ctx.AsActionContext(),
                    itemId,
                    int.TryParse(position[0], out var xPos) ? xPos : 0,
                    int.TryParse(position[1], out var yPos) ? yPos : 0,
                    int.TryParse(position[2], out var rotation)
                        ? (Rotation)rotation
                        : Rotation.North,
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
