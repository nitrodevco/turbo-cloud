using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// The wired menu's Reload and Rollback. The room service does it (<see cref="IRoomService.ReloadWiredRoomAsync"/>):
/// the room writes what it keeps, everyone is closed out and forwarded back in two seconds later.
/// </summary>
public class WiredUpdateRoomMessageHandler(IRoomService roomService)
    : IMessageHandler<WiredUpdateRoomMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        WiredUpdateRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        await _roomService
            .ReloadWiredRoomAsync(ctx.AsActionContext(), message.Rollback, ct)
            .ConfigureAwait(false);
    }
}
