using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Session;

/// <summary>
/// Direct entry request: home room button, a submitted door password, or a doorbell ring.
/// </summary>
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
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var access = await _roomService
            .CheckRoomEntryAccessAsync(
                ctx.PlayerId,
                message.RoomId,
                message.Password,
                bypassDoor: false,
                ct
            )
            .ConfigureAwait(false);

        await _roomService
            .OpenRoomForPlayerIdAsync(
                ctx.AsActionContext(),
                ctx.PlayerId,
                message.RoomId,
                access,
                ct
            )
            .ConfigureAwait(false);
    }
}
