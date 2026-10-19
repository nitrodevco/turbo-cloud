using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// A post-it pad dragged out of the inventory onto a wall (Flash sends it for a stickie in place
/// of <c>PlaceObject</c>): one sheet of the pad becomes a note there. The location string is the
/// usual wall format; the room reads it.
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

        await _roomService
            .PlacePostItInRoomAsync(ctx.AsActionContext(), message.ItemId, message.Location, ct)
            .ConfigureAwait(false);
    }
}
