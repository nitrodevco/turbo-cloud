using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Engine;

/// <summary>
/// Wall items share the floor use path; the item logic decides what a use means.
/// </summary>
public class UseWallItemMessageHandler(IRoomService roomService)
    : IMessageHandler<UseWallItemMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        UseWallItemMessage message,
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
