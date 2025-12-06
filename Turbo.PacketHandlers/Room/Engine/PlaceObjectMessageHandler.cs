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
        var position = message.Data.Split(' ');

        if (position.Length == 4)
        {
            var itemId = int.TryParse(position[0], out var id) ? Math.Abs(id) : -1;
            var x = int.TryParse(position[1], out var xPos) ? xPos : 0;
            var y = int.TryParse(position[2], out var yPos) ? yPos : 0;
            var rot = int.TryParse(position[3], out var rotation)
                ? (Rotation)rotation
                : Rotation.North;

            await _roomService
                .PlaceFloorItemInRoomAsync(ctx.AsActionContext(), itemId, x, y, rot, ct)
                .ConfigureAwait(false);
        }

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
