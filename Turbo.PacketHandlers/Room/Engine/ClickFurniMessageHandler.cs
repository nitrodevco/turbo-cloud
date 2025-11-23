using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Engine;

public class ClickFurniMessageHandler(IRoomService roomService) : IMessageHandler<ClickFurniMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        ClickFurniMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var isFloorItemClicked = message.ObjectId > 0;
        var isWallItemClicked = message.ObjectId < 0;

        var exec = ActorContext.ForPlayer(ctx.Session.SessionKey, ctx.PlayerId, ctx.RoomId);

        if (isFloorItemClicked)
        {
            await _roomService
                .ClickFloorItemInRoomAsync(exec, message.ObjectId, message.Param, ct)
                .ConfigureAwait(false);
        }
        else if (isWallItemClicked) { }
    }
}
