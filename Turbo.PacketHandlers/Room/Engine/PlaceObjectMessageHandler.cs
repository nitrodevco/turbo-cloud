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

        var position = message.Data.Split(' ');

        if (position.Length != 4)
            return;

        var itemId = int.TryParse(position[0], out var id) ? Math.Abs(id) : -1;
        var location = string.Join(' ', new[] { position[1], position[2], position[3] });

        position = location.Split(' ');

        if (location.StartsWith(':'))
        {
            var coords = position[0][3..].Split(',');
            var loc = position[1][2..].Split(',');
            var rot = position[2].Equals("l") ? Rotation.South : Rotation.West;

            if (coords.Length != 2 || loc.Length != 2)
                return;

            await _roomService
                .PlaceWallItemInRoomAsync(
                    ctx.AsActionContext(),
                    itemId,
                    int.TryParse(coords[0], out var x) ? x : 0,
                    int.TryParse(coords[1], out var y) ? y : 0,
                    double.TryParse(loc[1], out var z) ? z : 0,
                    int.TryParse(loc[0], out var wallOffset) ? wallOffset : 0,
                    rot,
                    ct
                )
                .ConfigureAwait(false);
        }
        else
        {
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
