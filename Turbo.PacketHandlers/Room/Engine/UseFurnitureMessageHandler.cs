using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.PacketHandlers.Room.Engine;

public class UseFurnitureMessageHandler(IRoomService roomService)
    : IMessageHandler<UseFurnitureMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        UseFurnitureMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _roomService
            .UseFloorItemInRoomAsync(
                ctx.AsActionContext(),
                RoomObjectId.From(message.ObjectId),
                ct,
                message.Param
            )
            .ConfigureAwait(false);
    }
}
