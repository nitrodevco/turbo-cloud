using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Session;

public class OpenFlatConnectionMessageHandler(IRoomService roomService)
    : IMessageHandler<OpenFlatConnectionMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        OpenFlatConnectionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _roomService
            .OpenRoomForPlayerIdAsync(ctx.Session.PlayerId, message.RoomId, ct)
            .ConfigureAwait(false);
    }
}
