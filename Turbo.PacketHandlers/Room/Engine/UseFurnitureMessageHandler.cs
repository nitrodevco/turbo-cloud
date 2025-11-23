using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Actor;
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
        var exec = ActorContextFactory.ForPlayer(ctx.Session.SessionKey, ctx.PlayerId, ctx.RoomId);

        await _roomService
            .UseFloorItemInRoomAsync(exec, message.ObjectId, message.Param, ct)
            .ConfigureAwait(false);
    }
}
