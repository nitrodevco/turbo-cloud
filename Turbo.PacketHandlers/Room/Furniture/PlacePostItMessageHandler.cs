using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// A post-it dragged out of the inventory onto a wall; the location string is the usual wall format.
/// </summary>
public class PlacePostItMessageHandler(IRoomService roomService)
    : IMessageHandler<PlacePostItMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        PlacePostItMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ItemId <= 0)
            return;

        if (!WallPosition.TryParse(message.Location, out var position))
            return;

        await _roomService
            .PlaceWallItemInRoomAsync(
                ctx.AsActionContext(),
                message.ItemId,
                position.X,
                position.Y,
                position.Z,
                position.WallOffset,
                position.Rotation,
                ct
            )
            .ConfigureAwait(false);
    }
}
