using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;

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
            .UseFloorItemInRoomAsync(ctx.PlayerId, ctx.RoomId, message.ObjectId, message.Param, ct)
            .ConfigureAwait(false);
    }
}
