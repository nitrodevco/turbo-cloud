using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// Deletes the room the owner is standing in. The client only offers this from inside the room.
/// </summary>
public class DeleteRoomMessageHandler(IRoomService roomService) : IMessageHandler<DeleteRoomMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        DeleteRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0 || message.RoomId != ctx.RoomId)
            return;

        await _roomService.DeleteRoomAsync(ctx.AsActionContext(), ct).ConfigureAwait(false);
    }
}
