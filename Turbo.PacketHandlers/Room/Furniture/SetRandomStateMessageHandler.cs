using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// The client sends this instead of UseFurniture for random-state furni; the logic picks the state.
/// </summary>
public class SetRandomStateMessageHandler(IRoomService roomService)
    : IMessageHandler<SetRandomStateMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        SetRandomStateMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        await _roomService
            .UseItemInRoomAsync(ctx.AsActionContext(), message.ObjectId, ct, message.Param)
            .ConfigureAwait(false);
    }
}
